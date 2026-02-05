#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Media.Playlist;

/// <summary>
/// Handles building Bible tracks for playlists.
/// Lookup path is always loaded from the media index (we only play harvested tracks).
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

    public record TrackInfo(string? SectionCode, BiblePublicationTrack Track, string Url);

    public async Task<List<PlayItem>> BuildBiblePublicationTracks(
        int scheduleId,
        AlarmSchedule schedule,
        BiblePublicationSchedule biblePublicationSchedule,
        Func<string, string, string?, int, Task<KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>>> getNextBiblePublicationTrackAsync)
    {
        var initialTrackInfo = await GetInitialTrackInfo(biblePublicationSchedule);
        var result = new List<PlayItem>();
        var isIndefinite = schedule.NumberOfTracksToPlay <= 0;
        // In indefinite mode, we still need to return the first playable track for playback and caching.
        // Subsequent tracks are resolved dynamically during playback.
        var numberOfTracksToRead = isIndefinite ? 1 : schedule.NumberOfTracksToPlay;
        var markedSeekTrack = false;

        var currentSectionCode = initialTrackInfo.SectionCode;
        var currentTrack = initialTrackInfo.Track;
        var currentUrl = initialTrackInfo.Url;

        while (numberOfTracksToRead > 0)
        {
            var (trackMetadata, updatedMarkedSeekTrack) = await CreateTrackMetadataAsync(
                scheduleId,
                biblePublicationSchedule,
                currentSectionCode,
                currentTrack.Number,
                numberOfTracksToRead,
                schedule,
                markedSeekTrack,
                isIndefinite);
            markedSeekTrack = updatedMarkedSeekTrack;

            result.Add(new PlayItem(trackMetadata, currentUrl));

            numberOfTracksToRead--;
            if (numberOfTracksToRead > 0)
            {
                var next = await GetNextTrackInfo(biblePublicationSchedule, currentSectionCode, currentTrack.Number, getNextBiblePublicationTrackAsync);
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
        int currentTrackNumber)
    {
        // Non-sectioned publication
        if (string.IsNullOrWhiteSpace(sectionCode))
        {
            return await GetAvailableTracksCountForNonSectioned(languageCode, publicationCode, currentTrackNumber);
        }

        // Sectioned publication
        return await GetAvailableTracksCountForSectioned(languageCode, publicationCode, sectionCode, currentTrackNumber);
    }

    private async Task<int> GetAvailableTracksCountForNonSectioned(
        string languageCode,
        string publicationCode,
        int currentTrackNumber)
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

        // Count tracks from current track number to the end (inclusive of current)
        var orderedTracks = publication.Tracks.OrderBy(t => t.Number).ToList();
        var remainingTracks = orderedTracks.Count(t => t.Number >= currentTrackNumber);

        return remainingTracks > 0 ? remainingTracks : orderedTracks.Count;
    }

    private async Task<int> GetAvailableTracksCountForSectioned(
        string languageCode,
        string publicationCode,
        string sectionCode,
        int currentTrackNumber)
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
        var remainingInCurrentSection = currentSectionTracks.Count(kvp => kvp.Key >= currentTrackNumber);
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

        var languageCode = biblePublicationSchedule.LanguageCode ?? "E";
        var tracks = await mediaService.GetBiblePublicationTracks(
            languageCode,
            biblePublicationSchedule.PublicationCode,
            biblePublicationSchedule.SectionCode);

        if (!tracks.TryGetValue(biblePublicationSchedule.TrackNumber, out var trackDetail))
        {
            logger.Error(
                $"Track: ${biblePublicationSchedule.TrackNumber}, sectionCode: {biblePublicationSchedule.SectionCode}, language: {biblePublicationSchedule.LanguageCode}, pub code: {biblePublicationSchedule.PublicationCode} not in lookup.");
            throw new InvalidOperationException($"Track {biblePublicationSchedule.TrackNumber} not found in section {biblePublicationSchedule.SectionCode}");
        }

        var sectionCode = biblePublicationSchedule.SectionCode;

        // Check if this is a no-language publication (e.g., instrumental music)
        // For no-language publications, we use "E" for both URL construction and API response parsing
        var isNoLanguagePublication = biblePublicationService != null &&
            await biblePublicationService.IsNoLanguagePublicationAsync(biblePublicationSchedule.PublicationCode);
        var effectiveLanguageCode = isNoLanguagePublication ? "E" : (biblePublicationSchedule.LanguageCode ?? "E");

        // Compute URL on-demand using TrackMetadata
        var trackMetadata = new TrackMetadata
        {
            IsBibleContent = true,
            LanguageCode = effectiveLanguageCode,
            PublicationCode = biblePublicationSchedule.PublicationCode,
            SectionCode = sectionCode,
            TrackNumber = trackDetail.Number
        };

        // Lookup path from media index only (we only play harvested tracks)
        var lookUpPath = await urlConstructionService.ConstructTrackLookUpPathAsync(
            biblePublicationSchedule.PublicationCode,
            effectiveLanguageCode,
            sectionCode,
            trackDetail.Number);
        if (string.IsNullOrEmpty(lookUpPath))
        {
            throw new InvalidOperationException(
                $"Track not found in media index: pub={biblePublicationSchedule.PublicationCode}, lang={effectiveLanguageCode}, section={sectionCode ?? "(none)"}, track={trackDetail.Number}. Only harvested tracks can be played.");
        }
        trackMetadata.LookUpPath = lookUpPath;

        // So alarm modal shows full track title for disc-style melody (e.g. iam): DisplayMetadataService needs DownloadCode/OriginalTrackNumber.
        ApplyDiscStyleDisplayMetadata(trackMetadata, sectionCode, trackDetail.Number);

        var url = await urlRefreshService.RefreshUrlAsync(trackMetadata);
        if (string.IsNullOrEmpty(url))
        {
            throw new InvalidOperationException($"Failed to get URL for track {biblePublicationSchedule.TrackNumber} in section {sectionCode ?? "(none)"}");
        }

        return new TrackInfo(sectionCode, trackDetail, url);
    }

    /// <summary>
    /// Sets DownloadCode and OriginalTrackNumber when section is disc-style (e.g. iam-9).
    /// DisplayMetadataService uses these to resolve melody track title in the alarm modal.
    /// </summary>
    private static void ApplyDiscStyleDisplayMetadata(TrackMetadata trackMetadata, string? sectionCode, int trackNumber)
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
        trackMetadata.OriginalTrackNumber = trackNumber;
    }

    private async Task<TrackInfo> GetInitialTrackInfoForNonSectionedPublication(BiblePublicationSchedule biblePublicationSchedule)
    {
        if (biblePublicationService == null)
        {
            throw new InvalidOperationException("IBiblePublicationService is required for non-sectioned publications");
        }

        var languageCode = biblePublicationSchedule.LanguageCode ?? "E";
        var publication = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(
            languageCode,
            biblePublicationSchedule.PublicationCode);

        if (publication == null || publication.Tracks == null || publication.Tracks.Count == 0)
        {
            throw new InvalidOperationException($"No tracks found for non-sectioned publication: {biblePublicationSchedule.LanguageCode}/{biblePublicationSchedule.PublicationCode}");
        }

        var track = publication.Tracks.FirstOrDefault(t => t.Number == biblePublicationSchedule.TrackNumber);
        if (track == null)
        {
            logger.Error("Track: {TrackNumber}, language: {LanguageCode}, pub code: {PublicationCode} not found in non-sectioned publication.",
                biblePublicationSchedule.TrackNumber, biblePublicationSchedule.LanguageCode, biblePublicationSchedule.PublicationCode);
            throw new InvalidOperationException($"Track {biblePublicationSchedule.TrackNumber} not found in non-sectioned publication");
        }

        // Compute URL on-demand using TrackMetadata
        var trackMetadata = new TrackMetadata
        {
            IsBibleContent = true,
            LanguageCode = languageCode,
            PublicationCode = biblePublicationSchedule.PublicationCode,
            SectionCode = null, // Non-sectioned
            TrackNumber = track.Number
        };

        // Lookup path from media index only (we only play harvested tracks)
        var lookUpPath = await urlConstructionService.ConstructTrackLookUpPathAsync(
            biblePublicationSchedule.PublicationCode,
            biblePublicationSchedule.LanguageCode,
            null, // No section for non-sectioned publications
            track.Number);
        if (string.IsNullOrEmpty(lookUpPath))
        {
            throw new InvalidOperationException(
                $"Track not found in media index: pub={biblePublicationSchedule.PublicationCode}, lang={biblePublicationSchedule.LanguageCode}, track={track.Number}. Only harvested tracks can be played.");
        }
        trackMetadata.LookUpPath = lookUpPath;

        var url = await urlRefreshService.RefreshUrlAsync(trackMetadata);
        if (string.IsNullOrEmpty(url))
        {
            throw new InvalidOperationException($"Failed to get URL for track {biblePublicationSchedule.TrackNumber} in non-sectioned publication");
        }

        return new TrackInfo(null, track, url);
    }

    private async Task<(TrackMetadata TrackMetadata, bool MarkedSeekTrack)> CreateTrackMetadataAsync(
        int scheduleId,
        BiblePublicationSchedule biblePublicationSchedule,
        string? sectionCode,
        int trackNumber,
        int remainingTracks,
        AlarmSchedule schedule,
        bool markedSeekTrack,
        bool isIndefinite)
    {
        // Check if this is a no-language publication (e.g., instrumental music)
        var isNoLanguagePublication = biblePublicationService != null &&
            await biblePublicationService.IsNoLanguagePublicationAsync(biblePublicationSchedule.PublicationCode);
        var effectiveLanguageCode = isNoLanguagePublication ? "E" : (biblePublicationSchedule.LanguageCode ?? "E");

        var trackMetadata = new TrackMetadata
        {
            ScheduleId = scheduleId,
            IsBibleContent = true,
            PublicationCode = biblePublicationSchedule.PublicationCode,
            LanguageCode = effectiveLanguageCode,
            SectionCode = sectionCode,
            TrackNumber = trackNumber,
            // In finite mode, mark the last track so playback stops/dismisses after the session.
            // In indefinite mode, never mark a track as last.
            IsLastTrack = !isIndefinite && remainingTracks == 1
        };

        // Lookup path from media index only (we only play harvested tracks)
        var lookUpPath = await urlConstructionService.ConstructTrackLookUpPathAsync(
            biblePublicationSchedule.PublicationCode,
            effectiveLanguageCode,
            sectionCode,
            trackNumber);
        if (string.IsNullOrEmpty(lookUpPath))
        {
            throw new InvalidOperationException(
                $"Track not found in media index: pub={biblePublicationSchedule.PublicationCode}, lang={effectiveLanguageCode}, section={sectionCode ?? "(none)"}, track={trackNumber}. Only harvested tracks can be played.");
        }
        trackMetadata.LookUpPath = lookUpPath;

        // So alarm modal shows full track title for disc-style melody (e.g. iam): DisplayMetadataService needs DownloadCode/OriginalTrackNumber.
        ApplyDiscStyleDisplayMetadata(trackMetadata, sectionCode, trackNumber);

        var shouldSet = ShouldSetFinishedDuration(markedSeekTrack, schedule, biblePublicationSchedule, trackMetadata, sectionCode);
        logger.Debug("[PlaylistBuild] ShouldSetFinishedDuration: {ShouldSet}, markedSeekTrack: {MarkedSeekTrack}, AlwaysPlayFromStart: {AlwaysPlayFromStart}, ScheduleFinishedDuration: {ScheduleFinishedDuration}, SectionMatch: {SectionMatch}",
            shouldSet,
            markedSeekTrack,
            schedule.AlwaysPlayFromStart,
            biblePublicationSchedule.FinishedDuration,
            string.Equals(sectionCode, trackMetadata.SectionCode, StringComparison.OrdinalIgnoreCase));

        if (shouldSet)
        {
            trackMetadata.FinishedDuration = biblePublicationSchedule.FinishedDuration;
            markedSeekTrack = true;
            logger.Debug("[PlaylistBuild] Set track FinishedDuration to {Duration}", trackMetadata.FinishedDuration);
        }

        return (trackMetadata, markedSeekTrack);
    }

    private static bool ShouldSetFinishedDuration(
        bool markedSeekTrack,
        AlarmSchedule schedule,
        BiblePublicationSchedule biblePublicationSchedule,
        TrackMetadata trackMetadata,
        string? sectionCode)
    {
        return !markedSeekTrack &&
               !schedule.AlwaysPlayFromStart &&
               !biblePublicationSchedule.FinishedDuration.Equals(TimeSpan.Zero) &&
               biblePublicationSchedule.LanguageCode == trackMetadata.LanguageCode &&
               biblePublicationSchedule.PublicationCode == trackMetadata.PublicationCode &&
               string.Equals(sectionCode, trackMetadata.SectionCode, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<TrackInfo> GetNextTrackInfo(
        BiblePublicationSchedule biblePublicationSchedule,
        string? currentSectionCode,
        int currentTrackNumber,
        Func<string, string, string?, int, Task<KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>>> getNextBiblePublicationTrackAsync)
    {
        var languageCode = biblePublicationSchedule.LanguageCode ?? "E";
        var next = await getNextBiblePublicationTrackAsync(
            languageCode,
            biblePublicationSchedule.PublicationCode,
            currentSectionCode,
            currentTrackNumber);

        if (next.Value == null)
        {
            throw new InvalidOperationException("Next track Value is null");
        }

        // Compute URL on-demand using TrackMetadata
        var nextSectionCode = next.Key?.SectionCode;

        // Check if this is a no-language publication (e.g., instrumental music)
        var isNoLanguagePublication = biblePublicationService != null &&
            await biblePublicationService.IsNoLanguagePublicationAsync(biblePublicationSchedule.PublicationCode);
        var effectiveLanguageCode = isNoLanguagePublication ? "E" : (biblePublicationSchedule.LanguageCode ?? "E");

        var trackMetadata = new TrackMetadata
        {
            IsBibleContent = true,
            LanguageCode = effectiveLanguageCode,
            PublicationCode = biblePublicationSchedule.PublicationCode,
            SectionCode = nextSectionCode,
            TrackNumber = next.Value.Number
        };

        // Lookup path from media index only (we only play harvested tracks)
        var lookUpPath = await urlConstructionService.ConstructTrackLookUpPathAsync(
            biblePublicationSchedule.PublicationCode,
            effectiveLanguageCode,
            nextSectionCode,
            next.Value.Number);
        if (string.IsNullOrEmpty(lookUpPath))
        {
            throw new InvalidOperationException(
                $"Track not found in media index: pub={biblePublicationSchedule.PublicationCode}, lang={effectiveLanguageCode}, section={nextSectionCode ?? "(none)"}, track={next.Value.Number}. Only harvested tracks can be played.");
        }
        trackMetadata.LookUpPath = lookUpPath;

        var url = await urlRefreshService.RefreshUrlAsync(trackMetadata);
        if (string.IsNullOrEmpty(url))
        {
            throw new InvalidOperationException($"Failed to get URL for next track {next.Value.Number}");
        }

        // For non-sectioned publications, Key (section) will be null.
        return new TrackInfo(nextSectionCode, next.Value, url);
    }
}
