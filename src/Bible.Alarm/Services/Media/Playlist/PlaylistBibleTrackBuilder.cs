#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Media.Playlist;

/// <summary>
/// Handles building Bible tracks for playlists.
/// Lookup path is always loaded from the media index (we only play cataloged tracks).
/// </summary>
public class PlaylistBiblePublicationTrackBuilder
{
    private const string MissingSectionDiagnosticToken = "(none)";
    private const string LogNullPlaceholder = "(null)";

    private readonly ILogger logger;
    private readonly IMediaService mediaService;
    private readonly IBiblePublicationService? biblePublicationService;
    private readonly IMediaUrlRefreshService urlRefreshService;
    private readonly IUrlConstructionService urlConstructionService;

    public PlaylistBiblePublicationTrackBuilder(
        ILogger logger,
        IMediaService mediaService,
        IMediaUrlRefreshService urlRefreshService,
        IUrlConstructionService urlConstructionService,
        IBiblePublicationService? biblePublicationService = null)
    {
        this.logger = logger;
        this.mediaService = mediaService;
        this.urlRefreshService = urlRefreshService;
        this.urlConstructionService = urlConstructionService;
        this.biblePublicationService = biblePublicationService;
    }

    public record TrackInfo(string PublicationCode, string? SectionCode, BiblePublicationTrack Track, string Url);

    private sealed record CreateTrackMetadataScheduleContext(
        int ScheduleId,
        BiblePublicationSchedule BiblePublicationSchedule,
        AlarmSchedule Schedule,
        int RemainingTracks,
        bool MarkedSeekTrack,
        bool IsIndefinite);

    private sealed record CreateTrackMetadataIdentity(
        string PublicationCode,
        string? SectionCode,
        string TrackCode);

    public async Task<List<PlayItem>> BuildBiblePublicationTracks(
        int scheduleId,
        AlarmSchedule schedule,
        BiblePublicationSchedule biblePublicationSchedule,
        Func<string, string, string?, string, Task<TrackNavigationResult>> getNextBiblePublicationTrackAsync)
    {
        var initialTrackInfo = await GetInitialTrackInfo(biblePublicationSchedule);
        var result = new List<PlayItem>();
        var isIndefinite = schedule.NumberOfTracksToPlay <= 0;
        // In indefinite mode, we still need to return the first playable track for playback and caching.
        // Subsequent tracks are resolved dynamically during playback.
        var numberOfTracksToRead = isIndefinite ? 1 : schedule.NumberOfTracksToPlay;
        var markedSeekTrack = false;

        var currentPublicationCode = initialTrackInfo.PublicationCode;
        var currentSectionCode = initialTrackInfo.SectionCode;
        var currentTrack = initialTrackInfo.Track;
        var currentUrl = initialTrackInfo.Url;

        while (numberOfTracksToRead > 0)
        {
            var (trackMetadata, updatedMarkedSeekTrack) = await CreateTrackMetadataAsync(
                new CreateTrackMetadataScheduleContext(
                    scheduleId,
                    biblePublicationSchedule,
                    schedule,
                    numberOfTracksToRead,
                    markedSeekTrack,
                    isIndefinite),
                new CreateTrackMetadataIdentity(
                    currentPublicationCode,
                    currentSectionCode,
                    TrackCodeHelper.GetFromTrack(currentTrack)));
            markedSeekTrack = updatedMarkedSeekTrack;

            result.Add(new PlayItem(trackMetadata, currentUrl));

            numberOfTracksToRead--;
            if (numberOfTracksToRead > 0)
            {
                var next = await GetNextTrackInfo(biblePublicationSchedule, currentPublicationCode, currentSectionCode, TrackCodeHelper.GetFromTrack(currentTrack), getNextBiblePublicationTrackAsync);
                currentPublicationCode = next.PublicationCode;
                currentSectionCode = next.SectionCode;
                currentTrack = next.Track;
                currentUrl = next.Url;
            }
        }

        return result;
    }

    public async Task<TrackInfo> GetInitialTrackInfo(BiblePublicationSchedule biblePublicationSchedule)
    {
        // For non-sectioned publications, get tracks directly from publication
        if (string.IsNullOrWhiteSpace(biblePublicationSchedule.SectionCode))
        {
            return await GetInitialTrackInfoForNonSectionedPublication(biblePublicationSchedule);
        }

        logger.Debug(AppConstants.Logging.PlaylistBiblePublicationTrackBuilderDiagnosticsLog.SectionedSchedulePublicationSectionTrackCodes,
            biblePublicationSchedule.PublicationCode, biblePublicationSchedule.SectionCode, biblePublicationSchedule.TrackCode);

        var languageCode = biblePublicationSchedule.LanguageCode ?? AppConstants.Media.DefaultLanguageCode;
        var tracks = await mediaService.GetBiblePublicationTracks(
            languageCode,
            biblePublicationSchedule.PublicationCode,
            biblePublicationSchedule.SectionCode);

        var scheduleTrackCode = biblePublicationSchedule.TrackCode ?? string.Empty;
        BiblePublicationTrack? trackDetail = null;
        if (tracks.TryGetValue(scheduleTrackCode, out var t))
        {
            trackDetail = t;
        }
        if (trackDetail == null)
        {
            trackDetail = tracks.Values.FirstOrDefault(tr => TrackCodeHelper.GetFromTrack(tr) == scheduleTrackCode);
        }
        if (trackDetail == null)
        {
            logger.Error(
                AppConstants.Logging.PlaylistBiblePublicationTrackBuilderDiagnosticsLog.TrackNotInLookupSectioned,
                biblePublicationSchedule.TrackCode, biblePublicationSchedule.SectionCode, biblePublicationSchedule.LanguageCode, biblePublicationSchedule.PublicationCode);
            throw new InvalidOperationException($"Track {biblePublicationSchedule.TrackCode} not found in section {biblePublicationSchedule.SectionCode}");
        }

        var sectionCode = biblePublicationSchedule.SectionCode;
        var resolvedTrackCode = TrackCodeHelper.GetFromTrack(trackDetail);

        // Check if this is a no-language publication (e.g., instrumental music)
        // For no-language publications, we use "E" for both URL construction and API response parsing
        var isNoLanguagePublication = biblePublicationService != null &&
            await biblePublicationService.IsNoLanguagePublicationAsync(biblePublicationSchedule.PublicationCode);
        var effectiveLanguageCode = ResolvePlaybackLanguageCode(isNoLanguagePublication, biblePublicationSchedule.LanguageCode);

        // Compute URL on-demand using TrackMetadata
        var trackMetadata = new TrackMetadata
        {
            IsBibleContent = true,
            LanguageCode = effectiveLanguageCode,
            PublicationCode = biblePublicationSchedule.PublicationCode,
            SectionCode = sectionCode,
            TrackCode = resolvedTrackCode
        };

        // Lookup path from media index only (we only play cataloged tracks)
        var lookUpPath = await urlConstructionService.ConstructTrackLookUpPathAsync(
            biblePublicationSchedule.PublicationCode,
            effectiveLanguageCode,
            sectionCode,
            resolvedTrackCode);
        if (string.IsNullOrEmpty(lookUpPath))
        {
            throw new InvalidOperationException(
                $"Track not found in media index: pub={biblePublicationSchedule.PublicationCode}, lang={effectiveLanguageCode}, section={sectionCode ?? MissingSectionDiagnosticToken}, track={resolvedTrackCode}. Only cataloged tracks can be played.");
        }
        trackMetadata.LookUpPath = lookUpPath;

        // So alarm modal shows full track title for disc-style melody (e.g. iam): DisplayMetadataService needs DownloadCode/OriginalTrackCode.
        ApplyDiscStyleDisplayMetadata(trackMetadata, sectionCode, resolvedTrackCode);

        var url = await urlRefreshService.RefreshUrlAsync(trackMetadata);
        if (string.IsNullOrEmpty(url))
        {
            throw new InvalidOperationException($"Failed to get URL for track {biblePublicationSchedule.TrackCode} in section {sectionCode ?? MissingSectionDiagnosticToken}");
        }

        return new TrackInfo(biblePublicationSchedule.PublicationCode, sectionCode, trackDetail, url);
    }

    /// <summary>
    /// Sets DownloadCode and OriginalTrackCode when section is disc-style (e.g. iam-9).
    /// DisplayMetadataService uses these to resolve melody track title in the alarm modal.
    /// </summary>
    private static void ApplyDiscStyleDisplayMetadata(TrackMetadata trackMetadata, string? sectionCode, string trackCode)
    {
        var normalizedSectionCode = SectionCodeHelper.Normalize(sectionCode);
        if (string.IsNullOrWhiteSpace(normalizedSectionCode) || !normalizedSectionCode.Contains('-'))
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(trackMetadata.PublicationCode) ||
            !normalizedSectionCode.StartsWith(trackMetadata.PublicationCode + "-", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        var parts = normalizedSectionCode.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return;
        }
        var suffix = parts[^1];
        if (string.IsNullOrEmpty(suffix) || !suffix.All(char.IsDigit) || suffix.All(c => c == '0'))
        {
            return;
        }
        trackMetadata.DownloadCode = normalizedSectionCode;
        if (int.TryParse(trackCode, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
        {
            trackMetadata.OriginalTrackCode = parsed;
        }
    }

    private async Task<TrackInfo> GetInitialTrackInfoForNonSectionedPublication(BiblePublicationSchedule biblePublicationSchedule)
    {
        if (biblePublicationService == null)
        {
            throw new InvalidOperationException("IBiblePublicationService is required for non-sectioned publications");
        }

        var languageCode = biblePublicationSchedule.LanguageCode ?? AppConstants.Media.DefaultLanguageCode;
        var publication = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(
            languageCode,
            biblePublicationSchedule.PublicationCode);

        if (publication == null || publication.Tracks == null || publication.Tracks.Count == 0)
        {
            throw new InvalidOperationException($"No tracks found for non-sectioned publication: {biblePublicationSchedule.LanguageCode}/{biblePublicationSchedule.PublicationCode}");
        }

        logger.Debug(AppConstants.Logging.PlaylistBiblePublicationTrackBuilderDiagnosticsLog.NonSectionedSchedulePublicationSectionTrackCodes,
            biblePublicationSchedule.PublicationCode, biblePublicationSchedule.SectionCode ?? LogNullPlaceholder, biblePublicationSchedule.TrackCode);

        var scheduleTrackCode = biblePublicationSchedule.TrackCode ?? string.Empty;
        var track = publication.Tracks.FirstOrDefault(t => TrackCodeHelper.GetFromTrack(t) == scheduleTrackCode);
        if (track == null)
        {
            logger.Error(AppConstants.Logging.PlaylistBiblePublicationTrackBuilderDiagnosticsLog.TrackNotFoundNonSectionedPublication,
                biblePublicationSchedule.TrackCode, biblePublicationSchedule.LanguageCode, biblePublicationSchedule.PublicationCode);
            throw new InvalidOperationException($"Track {biblePublicationSchedule.TrackCode} not found in non-sectioned publication");
        }

        var resolvedTrackCode = TrackCodeHelper.GetFromTrack(track);
        // Compute URL on-demand using TrackMetadata
        var trackMetadata = new TrackMetadata
        {
            IsBibleContent = true,
            LanguageCode = languageCode,
            PublicationCode = biblePublicationSchedule.PublicationCode,
            SectionCode = null, // Non-sectioned
            TrackCode = resolvedTrackCode
        };

        // Lookup path from media index only (we only play cataloged tracks)
        var lookUpPath = await urlConstructionService.ConstructTrackLookUpPathAsync(
            biblePublicationSchedule.PublicationCode,
            biblePublicationSchedule.LanguageCode,
            null, // No section for non-sectioned publications
            resolvedTrackCode);
        if (string.IsNullOrEmpty(lookUpPath))
        {
            throw new InvalidOperationException(
                $"Track not found in media index: pub={biblePublicationSchedule.PublicationCode}, lang={biblePublicationSchedule.LanguageCode}, track={track.TrackCode}. Only cataloged tracks can be played.");
        }
        trackMetadata.LookUpPath = lookUpPath;

        var url = await urlRefreshService.RefreshUrlAsync(trackMetadata);
        if (string.IsNullOrEmpty(url))
        {
            throw new InvalidOperationException($"Failed to get URL for track {biblePublicationSchedule.TrackCode} in non-sectioned publication");
        }

        logger.Information(AppConstants.Logging.PlaylistBiblePublicationTrackBuilderDiagnosticsLog.ResolvedNonSectionedTrack,
            biblePublicationSchedule.TrackCode, track.TrackCode, track.Title, lookUpPath);

        return new TrackInfo(biblePublicationSchedule.PublicationCode, null, track, url);
    }

    private async Task<(TrackMetadata TrackMetadata, bool MarkedSeekTrack)> CreateTrackMetadataAsync(
        CreateTrackMetadataScheduleContext ctx,
        CreateTrackMetadataIdentity id)
    {
        // Check if this is a no-language publication (e.g., instrumental music)
        var isNoLanguagePublication = biblePublicationService != null &&
            await biblePublicationService.IsNoLanguagePublicationAsync(id.PublicationCode);
        var effectiveLanguageCode = ResolvePlaybackLanguageCode(isNoLanguagePublication, ctx.BiblePublicationSchedule.LanguageCode);

        var trackMetadata = new TrackMetadata
        {
            ScheduleId = ctx.ScheduleId,
            IsBibleContent = true,
            PublicationCode = id.PublicationCode,
            LanguageCode = effectiveLanguageCode,
            SectionCode = id.SectionCode,
            TrackCode = id.TrackCode,
            // In finite mode, mark the last track so playback stops/dismisses after the session.
            // In indefinite mode, never mark a track as last.
            IsLastTrack = !ctx.IsIndefinite && ctx.RemainingTracks == 1
        };

        // Lookup path from media index only (we only play cataloged tracks)
        var lookUpPath = await urlConstructionService.ConstructTrackLookUpPathAsync(
            id.PublicationCode,
            effectiveLanguageCode,
            id.SectionCode,
            id.TrackCode);
        if (string.IsNullOrEmpty(lookUpPath))
        {
            throw new InvalidOperationException(
                $"Track not found in media index: pub={id.PublicationCode}, lang={effectiveLanguageCode}, section={id.SectionCode ?? MissingSectionDiagnosticToken}, track={id.TrackCode}. Only cataloged tracks can be played.");
        }
        trackMetadata.LookUpPath = lookUpPath;

        // So alarm modal shows full track title for disc-style melody (e.g. iam): DisplayMetadataService needs DownloadCode/OriginalTrackCode.
        ApplyDiscStyleDisplayMetadata(trackMetadata, id.SectionCode, id.TrackCode);

        var markedSeekTrack = ctx.MarkedSeekTrack;
        var shouldSet = ShouldSetFinishedDuration(markedSeekTrack, ctx.Schedule, ctx.BiblePublicationSchedule, trackMetadata, id.SectionCode, isNoLanguagePublication);
        logger.Information(AppConstants.Logging.PlaylistBiblePublicationTrackBuilderDiagnosticsLog.ShouldSetFinishedDurationDetails,
            shouldSet,
            markedSeekTrack,
            ctx.Schedule.AlwaysPlayFromStart,
            ctx.BiblePublicationSchedule.FinishedDuration,
            ctx.ScheduleId,
            id.TrackCode,
            id.SectionCode ?? LogNullPlaceholder);

        if (shouldSet)
        {
            trackMetadata.FinishedDuration = ctx.BiblePublicationSchedule.FinishedDuration;
            markedSeekTrack = true;
            logger.Information(AppConstants.Logging.PlaylistBiblePublicationTrackBuilderDiagnosticsLog.SetTrackFinishedDurationForSchedule, trackMetadata.FinishedDuration, ctx.ScheduleId);
        }
        else if (!markedSeekTrack && ctx.BiblePublicationSchedule.FinishedDuration > TimeSpan.Zero)
        {
            logger.Warning(AppConstants.Logging.PlaylistBiblePublicationTrackBuilderDiagnosticsLog.FinishedDurationInDbButShouldSetFalse,
                ctx.BiblePublicationSchedule.FinishedDuration, ctx.ScheduleId, ctx.Schedule.AlwaysPlayFromStart);
        }

        return (trackMetadata, markedSeekTrack);
    }

    private static bool ShouldSetFinishedDuration(
        bool markedSeekTrack,
        AlarmSchedule schedule,
        BiblePublicationSchedule biblePublicationSchedule,
        TrackMetadata trackMetadata,
        string? sectionCode,
        bool isNoLanguagePublication)
    {
        var scheduleLang = biblePublicationSchedule.LanguageCode ?? AppConstants.Media.DefaultLanguageCode;
        var metadataLang = trackMetadata.LanguageCode ?? AppConstants.Media.DefaultLanguageCode;
        var languageMatches = isNoLanguagePublication ||
            string.Equals(scheduleLang, metadataLang, StringComparison.OrdinalIgnoreCase);
        return !markedSeekTrack &&
               !schedule.AlwaysPlayFromStart &&
               !biblePublicationSchedule.FinishedDuration.Equals(TimeSpan.Zero) &&
               languageMatches &&
               biblePublicationSchedule.PublicationCode == trackMetadata.PublicationCode &&
               string.Equals(sectionCode, trackMetadata.SectionCode, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<TrackInfo> GetNextTrackInfo(
        BiblePublicationSchedule biblePublicationSchedule,
        string currentPublicationCode,
        string? currentSectionCode,
        string currentTrackCode,
        Func<string, string, string?, string, Task<TrackNavigationResult>> getNextBiblePublicationTrackAsync)
    {
        var languageCode = biblePublicationSchedule.LanguageCode ?? AppConstants.Media.DefaultLanguageCode;
        var next = await getNextBiblePublicationTrackAsync(
            languageCode,
            currentPublicationCode,
            currentSectionCode,
            currentTrackCode);

        var resolvedPublicationCode = next.PublicationCode;
        var nextSectionCode = next.Section?.SectionCode;
        var nextTrackCode = TrackCodeHelper.GetFromTrack(next.Track);

        // Check if this is a no-language publication (e.g., instrumental music)
        var isNoLanguagePublication = biblePublicationService != null &&
            await biblePublicationService.IsNoLanguagePublicationAsync(resolvedPublicationCode);
        var effectiveLanguageCode = ResolvePlaybackLanguageCode(isNoLanguagePublication, biblePublicationSchedule.LanguageCode);

        var trackMetadata = new TrackMetadata
        {
            IsBibleContent = true,
            LanguageCode = effectiveLanguageCode,
            PublicationCode = resolvedPublicationCode,
            SectionCode = nextSectionCode,
            TrackCode = nextTrackCode
        };

        // Lookup path from media index only (we only play cataloged tracks)
        var lookUpPath = await urlConstructionService.ConstructTrackLookUpPathAsync(
            resolvedPublicationCode,
            effectiveLanguageCode,
            nextSectionCode,
            nextTrackCode);
        if (string.IsNullOrEmpty(lookUpPath))
        {
            throw new InvalidOperationException(
                $"Track not found in media index: pub={resolvedPublicationCode}, lang={effectiveLanguageCode}, section={nextSectionCode ?? MissingSectionDiagnosticToken}, track={nextTrackCode}. Only cataloged tracks can be played.");
        }
        trackMetadata.LookUpPath = lookUpPath;

        var url = await urlRefreshService.RefreshUrlAsync(trackMetadata);
        if (string.IsNullOrEmpty(url))
        {
            throw new InvalidOperationException($"Failed to get URL for next track {nextTrackCode}");
        }

        return new TrackInfo(resolvedPublicationCode, nextSectionCode, next.Track, url);
    }

    /// <summary>
    /// Uses default language code for melody/no-language publications; otherwise schedule language with fallback.
    /// </summary>
    private static string ResolvePlaybackLanguageCode(bool isNoLanguagePublication, string? scheduleLanguageCode)
    {
        if (isNoLanguagePublication)
        {
            return AppConstants.Media.DefaultLanguageCode;
        }

        return scheduleLanguageCode ?? AppConstants.Media.DefaultLanguageCode;
    }
}
