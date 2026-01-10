#nullable enable
using Bible;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;
using Serilog;

namespace Bible.Alarm.Services.Media.Playlist;

/// <summary>
/// Handles building Bible tracks for playlists.
/// Separated from PlaylistService for better modularity.
/// </summary>
public class PlaylistBibleTrackBuilder
{
    private readonly ILogger logger;
    private readonly IMediaService mediaService;

    public PlaylistBibleTrackBuilder(ILogger logger, IMediaService mediaService)
    {
        this.logger = logger;
        this.mediaService = mediaService;
    }

    public record TrackInfo(int SectionNumber, BiblePublicationTrack Track, string Url);

    public async Task<List<PlayItem>> BuildBibleTracks(
        int scheduleId,
        AlarmSchedule schedule,
        BibleReadingSchedule bibleReadingSchedule,
        Func<string, string, int, int, Task<KeyValuePair<BibleSection, BiblePublicationTrack>>> getNextBibleTrackAsync)
    {
        var initialTrackInfo = await GetInitialTrackInfo(bibleReadingSchedule);
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
                bibleReadingSchedule,
                currentSectionNumber,
                currentTrack.Number,
                numberOfTracksToRead,
                schedule,
                ref markedSeekTrack);

            result.Add(new PlayItem(trackMetadata, currentUrl));

            numberOfTracksToRead--;
            if (numberOfTracksToRead > 0)
            {
                var next = await GetNextTrackInfo(bibleReadingSchedule, currentSectionNumber, currentTrack.Number, getNextBibleTrackAsync);
                currentSectionNumber = next.SectionNumber;
                currentTrack = next.Track;
                currentUrl = next.Url;
            }
        }

        return result;
    }

    public async Task<TrackInfo> GetInitialTrackInfo(BibleReadingSchedule bibleReadingSchedule)
    {
        var sectionNumber = bibleReadingSchedule.SectionNumber ?? throw new InvalidOperationException("SectionNumber is null");
        
        var tracks = await mediaService.GetBibleTracks(
            bibleReadingSchedule.LanguageCode,
            bibleReadingSchedule.PublicationCode,
            sectionNumber);

        if (!tracks.TryGetValue(bibleReadingSchedule.TrackNumber, out var trackDetail))
        {
            logger.Error(
                $"Track: ${bibleReadingSchedule.TrackNumber}, section: {sectionNumber}, language: {bibleReadingSchedule.LanguageCode}, pub code: {bibleReadingSchedule.PublicationCode} not in lookup.");
            throw new InvalidOperationException($"Track {bibleReadingSchedule.TrackNumber} not found in section {sectionNumber}");
        }

        if (trackDetail.Source == null)
        {
            throw new InvalidOperationException($"Track {bibleReadingSchedule.TrackNumber} Source is null in section {sectionNumber}");
        }

        return new TrackInfo(
            sectionNumber,
            trackDetail,
            trackDetail.Source.Url);
    }

    private TrackMetadata CreateTrackMetadata(
        int scheduleId,
        BibleReadingSchedule bibleReadingSchedule,
        int sectionNumber,
        int trackNumber,
        int remainingTracks,
        AlarmSchedule schedule,
        ref bool markedSeekTrack)
    {
        var trackMetadata = new TrackMetadata
        {
            ScheduleId = scheduleId,
            PublicationCode = bibleReadingSchedule.PublicationCode,
            LanguageCode = bibleReadingSchedule.LanguageCode,
            // LookUpPath is now computed from LanguageCode, PublicationCode, SectionNumber, TrackNumber
            SectionNumber = sectionNumber,
            TrackNumber = trackNumber,
            IsLastTrack = remainingTracks == 1
        };

        var shouldSet = ShouldSetFinishedDuration(markedSeekTrack, schedule, bibleReadingSchedule, trackMetadata);
        logger.Debug("[PlaylistBuild] ShouldSetFinishedDuration: {ShouldSet}, markedSeekTrack: {MarkedSeekTrack}, AlwaysPlayFromStart: {AlwaysPlayFromStart}, ScheduleFinishedDuration: {ScheduleFinishedDuration}, SectionMatch: {SectionMatch}",
            shouldSet,
            markedSeekTrack,
            schedule.AlwaysPlayFromStart,
            bibleReadingSchedule.FinishedDuration,
            bibleReadingSchedule.SectionNumber == trackMetadata.SectionNumber);
            
        if (shouldSet)
        {
            trackMetadata.FinishedDuration = bibleReadingSchedule.FinishedDuration;
            markedSeekTrack = true;
            logger.Debug("[PlaylistBuild] Set track FinishedDuration to {Duration}", trackMetadata.FinishedDuration);
        }

        return trackMetadata;
    }

    private static bool ShouldSetFinishedDuration(
        bool markedSeekTrack,
        AlarmSchedule schedule,
        BibleReadingSchedule bibleReadingSchedule,
        TrackMetadata trackMetadata)
    {
        return !markedSeekTrack &&
               !schedule.AlwaysPlayFromStart &&
               !bibleReadingSchedule.FinishedDuration.Equals(TimeSpan.Zero) &&
               bibleReadingSchedule.LanguageCode == trackMetadata.LanguageCode &&
               bibleReadingSchedule.PublicationCode == trackMetadata.PublicationCode &&
               bibleReadingSchedule.SectionNumber == trackMetadata.SectionNumber;
    }

    public async Task<TrackInfo> GetNextTrackInfo(
        BibleReadingSchedule bibleReadingSchedule,
        int currentSectionNumber,
        int currentTrackNumber,
        Func<string, string, int, int, Task<KeyValuePair<BibleSection, BibleTrack>>> getNextBibleTrackAsync)
    {
        var next = await getNextBibleTrackAsync(
            bibleReadingSchedule.LanguageCode,
            bibleReadingSchedule.PublicationCode,
            currentSectionNumber,
            currentTrackNumber);

        if (next.Key == null || next.Value == null)
        {
            throw new InvalidOperationException("Next track Key or Value is null");
        }

        if (next.Value.Source == null)
        {
            throw new InvalidOperationException("Next track Source is null");
        }

        return new TrackInfo(
            next.Key.Number,
            next.Value,
            next.Value.Source.Url);
    }
}
