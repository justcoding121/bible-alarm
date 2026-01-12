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

    public PlaylistBiblePublicationTrackBuilder(ILogger logger, IMediaService mediaService, IBiblePublicationService? biblePublicationService = null)
    {
        this.logger = logger;
        this.mediaService = mediaService;
        this.biblePublicationService = biblePublicationService;
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

        var currentSectionNumber = initialTrackInfo.SectionNumber;
        var currentTrack = initialTrackInfo.Track;
        var currentUrl = initialTrackInfo.Url;

        while (numberOfTracksToRead > 0)
        {
            var trackMetadata = CreateTrackMetadata(
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

        if (trackDetail.Source == null)
        {
            throw new InvalidOperationException($"Track {biblePublicationSchedule.TrackNumber} Source is null in section {sectionNumber}");
        }

        return new TrackInfo(
            sectionNumber,
            trackDetail,
            trackDetail.Source.Url);
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

        if (track.Source == null)
        {
            throw new InvalidOperationException($"Track {biblePublicationSchedule.TrackNumber} Source is null in non-sectioned publication");
        }

        var url = track.Source.Url;
        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            logger.Error("Track {TrackNumber} has invalid URL: '{Url}'. BaseUrlEntity may not be loaded. UrlPath={UrlPath}",
                track.Number, url, track.Source.UrlPath);
            throw new InvalidOperationException($"Track {biblePublicationSchedule.TrackNumber} has invalid URL in non-sectioned publication. URL: {url}");
        }

        return new TrackInfo(
            0, // No section for non-sectioned publications
            track,
            url);
    }

    private TrackMetadata CreateTrackMetadata(
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
            PublicationCode = biblePublicationSchedule.PublicationCode,
            LanguageCode = biblePublicationSchedule.LanguageCode,
            // LookUpPath is now computed from LanguageCode, PublicationCode, SectionNumber, TrackNumber
            SectionNumber = sectionNumber,
            TrackNumber = trackNumber,
            IsLastTrack = remainingTracks == 1
        };

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

        if (next.Value.Source == null)
        {
            throw new InvalidOperationException("Next track Source is null");
        }

        // For non-sectioned publications, Key (section) will be null, use 0
        return new TrackInfo(
            next.Key?.Number ?? 0,
            next.Value,
            next.Value.Source.Url);
    }
}
