#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
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
                scheduleId,
                biblePublicationSchedule,
                currentPublicationCode,
                currentSectionCode,
                TrackCodeHelper.GetFromTrack(currentTrack),
                numberOfTracksToRead,
                schedule,
                markedSeekTrack,
                isIndefinite);
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

    /// <summary>
    /// Gets the number of remaining tracks available from the current track to the end of the publication.
    /// For non-sectioned publications: counts from current track to last track.
    /// For sectioned publications: counts from current track to last track in current section, then adds all tracks in subsequent sections.
    /// This prevents the progress from showing more tracks than actually available.
    /// </summary>
    private async Task<int> GetAvailableTracksCount(
        string languageCode,
        string publicationCode,
        string? sectionCode,
        string currentTrackCode)
    {
        // Non-sectioned publication
        if (string.IsNullOrWhiteSpace(sectionCode))
        {
            return await GetAvailableTracksCountForNonSectioned(languageCode, publicationCode, currentTrackCode);
        }

        // Sectioned publication
        return await GetAvailableTracksCountForSectioned(languageCode, publicationCode, sectionCode, currentTrackCode);
    }

    private async Task<int> GetAvailableTracksCountForNonSectioned(
        string languageCode,
        string publicationCode,
        string currentTrackCode)
    {
        if (biblePublicationService == null)
        {
            throw new InvalidOperationException("IBiblePublicationService is required for non-sectioned publications");
        }

        var publication = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(languageCode, publicationCode);

        if (publication?.Tracks == null || publication.Tracks.Count == 0)
        {
            return 0;
        }

        // Count tracks from current track code to the end (inclusive of current)
        var orderedTracks = publication.Tracks.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))).ToList();
        var remainingTracks = orderedTracks.Count(t => string.Compare(t.TrackCode, currentTrackCode, StringComparison.OrdinalIgnoreCase) >= 0);

        return remainingTracks > 0 ? remainingTracks : orderedTracks.Count;
    }

    private async Task<int> GetAvailableTracksCountForSectioned(
        string languageCode,
        string publicationCode,
        string sectionCode,
        string currentTrackCode)
    {
        var totalCount = 0;

        // Get all sections for this publication
        var sections = await mediaService.GetBiblePublicationSections(languageCode, publicationCode);
        if (sections.Count == 0)
        {
            return 0;
        }

        // Get tracks in current section from current track onwards
        var currentSectionTracks = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, sectionCode);
        var remainingInCurrentSection = currentSectionTracks.Count(kvp => string.Compare(kvp.Key, currentTrackCode, StringComparison.OrdinalIgnoreCase) >= 0);
        totalCount += remainingInCurrentSection;

        // Add all tracks from subsequent sections
        var keys = sections.Keys.ToList();
        var currentIndex = keys.FindIndex(k => string.Equals(k, sectionCode, StringComparison.OrdinalIgnoreCase));
        if (currentIndex < 0)
        {
            return totalCount;
        }

        for (var i = currentIndex + 1; i < keys.Count; i++)
        {
            var nextSectionCode = keys[i];
            var sectionTracks = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, nextSectionCode);
            totalCount += sectionTracks.Count;
        }

        return totalCount;
    }

    public async Task<TrackInfo> GetInitialTrackInfo(BiblePublicationSchedule biblePublicationSchedule)
    {
        // For non-sectioned publications, get tracks directly from publication
        if (string.IsNullOrWhiteSpace(biblePublicationSchedule.SectionCode))
        {
            return await GetInitialTrackInfoForNonSectionedPublication(biblePublicationSchedule);
        }

        logger.Debug("[PlaylistBuild] Sectioned schedule: PublicationCode={PublicationCode}, SectionCode={SectionCode}, TrackCode={TrackCode}",
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
                "Track: {TrackCode}, sectionCode: {SectionCode}, language: {LanguageCode}, pub code: {PublicationCode} not in lookup.",
                biblePublicationSchedule.TrackCode, biblePublicationSchedule.SectionCode, biblePublicationSchedule.LanguageCode, biblePublicationSchedule.PublicationCode);
            throw new InvalidOperationException($"Track {biblePublicationSchedule.TrackCode} not found in section {biblePublicationSchedule.SectionCode}");
        }

        var sectionCode = biblePublicationSchedule.SectionCode;
        var resolvedTrackCode = TrackCodeHelper.GetFromTrack(trackDetail);

        // Check if this is a no-language publication (e.g., instrumental music)
        // For no-language publications, we use "E" for both URL construction and API response parsing
        var isNoLanguagePublication = biblePublicationService != null &&
            await biblePublicationService.IsNoLanguagePublicationAsync(biblePublicationSchedule.PublicationCode);
        var effectiveLanguageCode = isNoLanguagePublication ? AppConstants.Media.DefaultLanguageCode : (biblePublicationSchedule.LanguageCode ?? AppConstants.Media.DefaultLanguageCode);

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
                $"Track not found in media index: pub={biblePublicationSchedule.PublicationCode}, lang={effectiveLanguageCode}, section={sectionCode ?? "(none)"}, track={resolvedTrackCode}. Only cataloged tracks can be played.");
        }
        trackMetadata.LookUpPath = lookUpPath;

        // So alarm modal shows full track title for disc-style melody (e.g. iam): DisplayMetadataService needs DownloadCode/OriginalTrackCode.
        ApplyDiscStyleDisplayMetadata(trackMetadata, sectionCode, resolvedTrackCode);

        var url = await urlRefreshService.RefreshUrlAsync(trackMetadata);
        if (string.IsNullOrEmpty(url))
        {
            throw new InvalidOperationException($"Failed to get URL for track {biblePublicationSchedule.TrackCode} in section {sectionCode ?? "(none)"}");
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

        logger.Debug("[PlaylistBuild] Non-sectioned schedule: PublicationCode={PublicationCode}, SectionCode={SectionCode}, TrackCode={TrackCode}",
            biblePublicationSchedule.PublicationCode, biblePublicationSchedule.SectionCode ?? "(null)", biblePublicationSchedule.TrackCode);

        var scheduleTrackCode = biblePublicationSchedule.TrackCode ?? string.Empty;
        var track = publication.Tracks.FirstOrDefault(t => TrackCodeHelper.GetFromTrack(t) == scheduleTrackCode);
        if (track == null)
        {
            logger.Error("Track: {TrackCode}, language: {LanguageCode}, pub code: {PublicationCode} not found in non-sectioned publication.",
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

        logger.Information("[PlaylistBuild] Resolved non-sectioned track: Schedule TrackCode={ScheduleTrackCode}, Resolved TrackCode={ResolvedTrackCode}, Title={Title}, LookUpPath={LookUpPath}",
            biblePublicationSchedule.TrackCode, track.TrackCode, track.Title, lookUpPath);

        return new TrackInfo(biblePublicationSchedule.PublicationCode, null, track, url);
    }

    private async Task<(TrackMetadata TrackMetadata, bool MarkedSeekTrack)> CreateTrackMetadataAsync(
        int scheduleId,
        BiblePublicationSchedule biblePublicationSchedule,
        string publicationCode,
        string? sectionCode,
        string trackCode,
        int remainingTracks,
        AlarmSchedule schedule,
        bool markedSeekTrack,
        bool isIndefinite)
    {
        // Check if this is a no-language publication (e.g., instrumental music)
        var isNoLanguagePublication = biblePublicationService != null &&
            await biblePublicationService.IsNoLanguagePublicationAsync(publicationCode);
        var effectiveLanguageCode = isNoLanguagePublication ? AppConstants.Media.DefaultLanguageCode : (biblePublicationSchedule.LanguageCode ?? AppConstants.Media.DefaultLanguageCode);

        var trackMetadata = new TrackMetadata
        {
            ScheduleId = scheduleId,
            IsBibleContent = true,
            PublicationCode = publicationCode,
            LanguageCode = effectiveLanguageCode,
            SectionCode = sectionCode,
            TrackCode = trackCode,
            // In finite mode, mark the last track so playback stops/dismisses after the session.
            // In indefinite mode, never mark a track as last.
            IsLastTrack = !isIndefinite && remainingTracks == 1
        };

        // Lookup path from media index only (we only play cataloged tracks)
        var lookUpPath = await urlConstructionService.ConstructTrackLookUpPathAsync(
            publicationCode,
            effectiveLanguageCode,
            sectionCode,
            trackCode);
        if (string.IsNullOrEmpty(lookUpPath))
        {
            throw new InvalidOperationException(
                $"Track not found in media index: pub={publicationCode}, lang={effectiveLanguageCode}, section={sectionCode ?? "(none)"}, track={trackCode}. Only cataloged tracks can be played.");
        }
        trackMetadata.LookUpPath = lookUpPath;

        // So alarm modal shows full track title for disc-style melody (e.g. iam): DisplayMetadataService needs DownloadCode/OriginalTrackCode.
        ApplyDiscStyleDisplayMetadata(trackMetadata, sectionCode, trackCode);

        var shouldSet = ShouldSetFinishedDuration(markedSeekTrack, schedule, biblePublicationSchedule, trackMetadata, sectionCode, isNoLanguagePublication);
        logger.Information("[PlaylistBuild] ShouldSetFinishedDuration={ShouldSet}: markedSeekTrack={MarkedSeekTrack}, AlwaysPlayFromStart={AlwaysPlayFromStart}, DB_FinishedDuration={ScheduleFinishedDuration}, ScheduleId={ScheduleId}, TrackCode={TrackCode}, SectionCode={SectionCode}",
            shouldSet,
            markedSeekTrack,
            schedule.AlwaysPlayFromStart,
            biblePublicationSchedule.FinishedDuration,
            scheduleId,
            trackCode,
            sectionCode ?? "(null)");

        if (shouldSet)
        {
            trackMetadata.FinishedDuration = biblePublicationSchedule.FinishedDuration;
            markedSeekTrack = true;
            logger.Information("[PlaylistBuild] Set track FinishedDuration={Duration} for ScheduleId={ScheduleId}", trackMetadata.FinishedDuration, scheduleId);
        }
        else if (!markedSeekTrack && biblePublicationSchedule.FinishedDuration > TimeSpan.Zero)
        {
            logger.Warning("[PlaylistBuild] FinishedDuration={Duration} in DB but ShouldSetFinishedDuration returned false for ScheduleId={ScheduleId}. AlwaysPlayFromStart={AlwaysPlayFromStart}",
                biblePublicationSchedule.FinishedDuration, scheduleId, schedule.AlwaysPlayFromStart);
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
        var effectiveLanguageCode = isNoLanguagePublication ? AppConstants.Media.DefaultLanguageCode : (biblePublicationSchedule.LanguageCode ?? AppConstants.Media.DefaultLanguageCode);

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
                $"Track not found in media index: pub={resolvedPublicationCode}, lang={effectiveLanguageCode}, section={nextSectionCode ?? "(none)"}, track={nextTrackCode}. Only cataloged tracks can be played.");
        }
        trackMetadata.LookUpPath = lookUpPath;

        var url = await urlRefreshService.RefreshUrlAsync(trackMetadata);
        if (string.IsNullOrEmpty(url))
        {
            throw new InvalidOperationException($"Failed to get URL for next track {nextTrackCode}");
        }

        return new TrackInfo(resolvedPublicationCode, nextSectionCode, next.Track, url);
    }
}
