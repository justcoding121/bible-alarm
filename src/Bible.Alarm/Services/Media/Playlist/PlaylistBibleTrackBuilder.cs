#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Media.Playlist;

/// <summary>
/// Handles building Bible tracks for playlists.
/// Separated from PlaylistService for better modularity.
/// </summary>
public class PlaylistBiblePublicationTrackBuilder
{
    private readonly ILogger logger;
    private readonly IMediaService mediaService;
    private readonly IBiblePublicationService? biblePublicationService;
    private readonly IMediaUrlRefreshService urlRefreshService;
    private readonly IUrlConstructionService? urlConstructionService;

    public PlaylistBiblePublicationTrackBuilder(
        ILogger logger, 
        IMediaService mediaService, 
        IMediaUrlRefreshService urlRefreshService, 
        IBiblePublicationService? biblePublicationService = null,
        IUrlConstructionService? urlConstructionService = null)
    {
        this.logger = logger;
        this.mediaService = mediaService;
        this.urlRefreshService = urlRefreshService;
        this.biblePublicationService = biblePublicationService;
        this.urlConstructionService = urlConstructionService;
    }

    public record TrackInfo(int SectionNumber, BiblePublicationTrack Track, string Url);

    public async Task<List<PlayItem>> BuildBiblePublicationTracks(
        int scheduleId,
        AlarmSchedule schedule,
        BiblePublicationSchedule biblePublicationSchedule,
        Func<string, string, int, int, Task<KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>>> getNextBiblePublicationTrackAsync)
    {
        var initialTrackInfo = await GetInitialTrackInfo(biblePublicationSchedule);
        var result = new List<PlayItem>();
        var numberOfTracksToRead = schedule.NumberOfTracksToRead;
        var markedSeekTrack = false;

        // Limit to available tracks to prevent wrapping/duplicates for both sectioned and non-sectioned publications
        var sectionNumber = biblePublicationSchedule.SectionNumber ?? 0;
        var availableTracksCount = await GetAvailableTracksCount(
            biblePublicationSchedule.LanguageCode,
            biblePublicationSchedule.PublicationCode,
            sectionNumber,
            biblePublicationSchedule.TrackNumber);

        if (availableTracksCount < numberOfTracksToRead)
        {
            logger.Debug("[PlaylistBuild] Limiting tracks from {Requested} to {Available} for publication (sectionNumber={Section})",
                numberOfTracksToRead, availableTracksCount, sectionNumber);
            numberOfTracksToRead = availableTracksCount;
        }

        var currentSectionNumber = initialTrackInfo.SectionNumber;
        var currentTrack = initialTrackInfo.Track;
        var currentUrl = initialTrackInfo.Url;

        while (numberOfTracksToRead > 0)
        {
            var trackMetadata = await CreateTrackMetadataAsync(
                scheduleId,
                biblePublicationSchedule,
                currentSectionNumber,
                currentTrack.Number,
                numberOfTracksToRead,
                schedule,
                ref markedSeekTrack);

            result.Add(new PlayItem(trackMetadata, currentUrl));

            numberOfTracksToRead--;
            if (numberOfTracksToRead > 0)
            {
                var next = await GetNextTrackInfo(biblePublicationSchedule, currentSectionNumber, currentTrack.Number, getNextBiblePublicationTrackAsync);
                currentSectionNumber = next.SectionNumber;
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
        int sectionNumber,
        int currentTrackNumber)
    {
        // Non-sectioned publication
        if (sectionNumber == 0)
        {
            return await GetAvailableTracksCountForNonSectioned(languageCode, publicationCode, currentTrackNumber);
        }

        // Sectioned publication
        return await GetAvailableTracksCountForSectioned(languageCode, publicationCode, sectionNumber, currentTrackNumber);
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
        int sectionNumber,
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
        var currentSectionTracks = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, sectionNumber);
        var remainingInCurrentSection = currentSectionTracks.Count(kvp => kvp.Key >= currentTrackNumber);
        totalCount += remainingInCurrentSection;

        // Add all tracks from subsequent sections
        foreach (var section in sections.Where(s => s.Key > sectionNumber))
        {
            var sectionTracks = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, section.Key);
            totalCount += sectionTracks.Count;
        }

        return totalCount;
    }

    public async Task<TrackInfo> GetInitialTrackInfo(BiblePublicationSchedule biblePublicationSchedule)
    {
        // Use 0 for non-sectioned publications
        var sectionNumber = biblePublicationSchedule.SectionNumber ?? 0;

        // For non-sectioned publications, get tracks directly from publication
        if (sectionNumber == 0)
        {
            return await GetInitialTrackInfoForNonSectionedPublication(biblePublicationSchedule);
        }

        var tracks = await mediaService.GetBiblePublicationTracks(
            biblePublicationSchedule.LanguageCode,
            biblePublicationSchedule.PublicationCode,
            sectionNumber);

        if (!tracks.TryGetValue(biblePublicationSchedule.TrackNumber, out var trackDetail))
        {
            logger.Error(
                $"Track: ${biblePublicationSchedule.TrackNumber}, section: {sectionNumber}, language: {biblePublicationSchedule.LanguageCode}, pub code: {biblePublicationSchedule.PublicationCode} not in lookup.");
            throw new InvalidOperationException($"Track {biblePublicationSchedule.TrackNumber} not found in section {sectionNumber}");
        }

        // Compute URL on-demand using TrackMetadata
        var trackMetadata = new TrackMetadata
        {
            IsBibleContent = true,
            LanguageCode = biblePublicationSchedule.LanguageCode,
            PublicationCode = biblePublicationSchedule.PublicationCode,
            SectionNumber = sectionNumber,
            TrackNumber = trackDetail.Number
        };

        // Use UrlConstructionService to get the lookup path from database
        if (urlConstructionService != null)
        {
            var lookUpPath = await urlConstructionService.ConstructTrackLookUpPathAsync(
                biblePublicationSchedule.PublicationCode,
                biblePublicationSchedule.LanguageCode,
                sectionNumber > 0 ? sectionNumber.ToString() : null,
                trackDetail.Number);
            if (!string.IsNullOrEmpty(lookUpPath))
            {
                trackMetadata.LookUpPath = lookUpPath;
            }
        }

        var url = await urlRefreshService.RefreshUrlAsync(trackMetadata);
        if (string.IsNullOrEmpty(url))
        {
            throw new InvalidOperationException($"Failed to get URL for track {biblePublicationSchedule.TrackNumber} in section {sectionNumber}");
        }

        return new TrackInfo(
            sectionNumber,
            trackDetail,
            url);
    }

    private async Task<TrackInfo> GetInitialTrackInfoForNonSectionedPublication(BiblePublicationSchedule biblePublicationSchedule)
    {
        if (biblePublicationService == null)
        {
            throw new InvalidOperationException("IBiblePublicationService is required for non-sectioned publications");
        }

        var publication = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(
            biblePublicationSchedule.LanguageCode,
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
            LanguageCode = biblePublicationSchedule.LanguageCode,
            PublicationCode = biblePublicationSchedule.PublicationCode,
            SectionNumber = 0, // Non-sectioned
            TrackNumber = track.Number
        };

        // Use UrlConstructionService to get the lookup path from database
        if (urlConstructionService != null)
        {
            var lookUpPath = await urlConstructionService.ConstructTrackLookUpPathAsync(
                biblePublicationSchedule.PublicationCode,
                biblePublicationSchedule.LanguageCode,
                null, // No section for non-sectioned publications
                track.Number);
            if (!string.IsNullOrEmpty(lookUpPath))
            {
                trackMetadata.LookUpPath = lookUpPath;
            }
        }

        var url = await urlRefreshService.RefreshUrlAsync(trackMetadata);
        if (string.IsNullOrEmpty(url))
        {
            throw new InvalidOperationException($"Failed to get URL for track {biblePublicationSchedule.TrackNumber} in non-sectioned publication");
        }

        return new TrackInfo(
            0, // No section for non-sectioned publications
            track,
            url);
    }

    private async Task<TrackMetadata> CreateTrackMetadataAsync(
        int scheduleId,
        BiblePublicationSchedule biblePublicationSchedule,
        int sectionNumber,
        int trackNumber,
        int remainingTracks,
        AlarmSchedule schedule,
        ref bool markedSeekTrack)
    {
        var trackMetadata = new TrackMetadata
        {
            ScheduleId = scheduleId,
            IsBibleContent = true,
            PublicationCode = biblePublicationSchedule.PublicationCode,
            LanguageCode = biblePublicationSchedule.LanguageCode,
            SectionNumber = sectionNumber,
            TrackNumber = trackNumber,
            IsLastTrack = remainingTracks == 1
        };

        // Use UrlConstructionService to get the lookup path from database
        if (urlConstructionService != null)
        {
            var lookUpPath = await urlConstructionService.ConstructTrackLookUpPathAsync(
                biblePublicationSchedule.PublicationCode,
                biblePublicationSchedule.LanguageCode,
                sectionNumber > 0 ? sectionNumber : null,
                trackNumber);
            if (!string.IsNullOrEmpty(lookUpPath))
            {
                trackMetadata.LookUpPath = lookUpPath;
            }
        }

        var shouldSet = ShouldSetFinishedDuration(markedSeekTrack, schedule, biblePublicationSchedule, trackMetadata);
        logger.Debug("[PlaylistBuild] ShouldSetFinishedDuration: {ShouldSet}, markedSeekTrack: {MarkedSeekTrack}, AlwaysPlayFromStart: {AlwaysPlayFromStart}, ScheduleFinishedDuration: {ScheduleFinishedDuration}, SectionMatch: {SectionMatch}",
            shouldSet,
            markedSeekTrack,
            schedule.AlwaysPlayFromStart,
            biblePublicationSchedule.FinishedDuration,
            biblePublicationSchedule.SectionNumber == trackMetadata.SectionNumber);

        if (shouldSet)
        {
            trackMetadata.FinishedDuration = biblePublicationSchedule.FinishedDuration;
            markedSeekTrack = true;
            logger.Debug("[PlaylistBuild] Set track FinishedDuration to {Duration}", trackMetadata.FinishedDuration);
        }

        return trackMetadata;
    }

    private static bool ShouldSetFinishedDuration(
        bool markedSeekTrack,
        AlarmSchedule schedule,
        BiblePublicationSchedule biblePublicationSchedule,
        TrackMetadata trackMetadata)
    {
        return !markedSeekTrack &&
               !schedule.AlwaysPlayFromStart &&
               !biblePublicationSchedule.FinishedDuration.Equals(TimeSpan.Zero) &&
               biblePublicationSchedule.LanguageCode == trackMetadata.LanguageCode &&
               biblePublicationSchedule.PublicationCode == trackMetadata.PublicationCode &&
               biblePublicationSchedule.SectionNumber == trackMetadata.SectionNumber;
    }

    public async Task<TrackInfo> GetNextTrackInfo(
        BiblePublicationSchedule biblePublicationSchedule,
        int currentSectionNumber,
        int currentTrackNumber,
        Func<string, string, int, int, Task<KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>>> getNextBiblePublicationTrackAsync)
    {
        var next = await getNextBiblePublicationTrackAsync(
            biblePublicationSchedule.LanguageCode,
            biblePublicationSchedule.PublicationCode,
            currentSectionNumber,
            currentTrackNumber);

        if (next.Value == null)
        {
            throw new InvalidOperationException("Next track Value is null");
        }

        // Compute URL on-demand using TrackMetadata
        var sectionNumber = next.Key?.Number ?? 0;
        var trackMetadata = new TrackMetadata
        {
            IsBibleContent = true,
            LanguageCode = biblePublicationSchedule.LanguageCode,
            PublicationCode = biblePublicationSchedule.PublicationCode,
            SectionNumber = sectionNumber,
            TrackNumber = next.Value.Number
        };

        // Use UrlConstructionService to get the lookup path from database
        if (urlConstructionService != null)
        {
            var lookUpPath = await urlConstructionService.ConstructTrackLookUpPathAsync(
                biblePublicationSchedule.PublicationCode,
                biblePublicationSchedule.LanguageCode,
                sectionNumber > 0 ? sectionNumber : null,
                next.Value.Number);
            if (!string.IsNullOrEmpty(lookUpPath))
            {
                trackMetadata.LookUpPath = lookUpPath;
            }
        }

        var url = await urlRefreshService.RefreshUrlAsync(trackMetadata);
        if (string.IsNullOrEmpty(url))
        {
            throw new InvalidOperationException($"Failed to get URL for next track {next.Value.Number}");
        }

        // For non-sectioned publications, Key (section) will be null, use 0
        return new TrackInfo(
            sectionNumber,
            next.Value,
            url);
    }
}
