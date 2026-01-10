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
        BiblePublicationSchedule biblePublicationSchedule,
        Func<string, string, int, int, Task<KeyValuePair<BibleSection, BiblePublicationTrack>>> getNextBibleTrackAsync)
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
                var next = await GetNextTrackInfo(biblePublicationSchedule, currentSectionNumber, currentTrack.Number, getNextBibleTrackAsync);
                currentSectionNumber = next.SectionNumber;
                currentTrack = next.Track;
                currentUrl = next.Url;
            }
        }

        return result;
    }

    public async Task<TrackInfo> GetInitialTrackInfo(BiblePublicationSchedule biblePublicationSchedule)
    {
        var sectionNumber = biblePublicationSchedule.SectionNumber ?? throw new InvalidOperationException("SectionNumber is null");
        
        var tracks = await mediaService.GetBibleTracks(
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
        Func<string, string, int, int, Task<KeyValuePair<BibleSection, BibleTrack>>> getNextBibleTrackAsync)
    {
        var next = await getNextBibleTrackAsync(
            biblePublicationSchedule.LanguageCode,
            biblePublicationSchedule.PublicationCode,
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
