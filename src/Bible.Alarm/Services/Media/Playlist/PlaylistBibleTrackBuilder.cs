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

    public record ChapterInfo(int SectionNumber, BiblePublicationChapter Chapter, string Url);

    public async Task<List<PlayItem>> BuildBibleTracks(
        int scheduleId,
        AlarmSchedule schedule,
        BibleReadingSchedule bibleReadingSchedule,
        Func<string, string, int, int, Task<KeyValuePair<BibleSection, BiblePublicationChapter>>> getNextBibleChapterAsync)
    {
        var initialChapterInfo = await GetInitialChapterInfo(bibleReadingSchedule);
        var result = new List<PlayItem>();
        var numberOfChaptersToRead = schedule.NumberOfChaptersToRead;
        var markedSeekTrack = false;

        var currentSectionNumber = initialChapterInfo.SectionNumber;
        var currentChapter = initialChapterInfo.Chapter;
        var currentUrl = initialChapterInfo.Url;

        while (numberOfChaptersToRead > 0)
        {
            var trackMetadata = CreateTrackMetadata(
                scheduleId,
                bibleReadingSchedule,
                currentSectionNumber,
                currentChapter.Number,
                numberOfChaptersToRead,
                schedule,
                ref markedSeekTrack);

            result.Add(new PlayItem(trackMetadata, currentUrl));

            numberOfChaptersToRead--;
            if (numberOfChaptersToRead > 0)
            {
                var next = await GetNextChapterInfo(bibleReadingSchedule, currentSectionNumber, currentChapter.Number, getNextBibleChapterAsync);
                currentSectionNumber = next.SectionNumber;
                currentChapter = next.Chapter;
                currentUrl = next.Url;
            }
        }

        return result;
    }

    public async Task<ChapterInfo> GetInitialChapterInfo(BibleReadingSchedule bibleReadingSchedule)
    {
        var sectionNumber = bibleReadingSchedule.SectionNumber ?? throw new InvalidOperationException("SectionNumber is null");
        
        var chapters = await mediaService.GetBibleChapters(
            bibleReadingSchedule.LanguageCode,
            bibleReadingSchedule.PublicationCode,
            sectionNumber);

        if (!chapters.TryGetValue(bibleReadingSchedule.ChapterNumber, out var chapterDetail))
        {
            logger.Error(
                $"Chapter: ${bibleReadingSchedule.ChapterNumber}, section: {sectionNumber}, language: {bibleReadingSchedule.LanguageCode}, pub code: {bibleReadingSchedule.PublicationCode} not in lookup.");
            throw new InvalidOperationException($"Chapter {bibleReadingSchedule.ChapterNumber} not found in section {sectionNumber}");
        }

        if (chapterDetail.Source == null)
        {
            throw new InvalidOperationException($"Chapter {bibleReadingSchedule.ChapterNumber} Source is null in section {sectionNumber}");
        }

        return new ChapterInfo(
            sectionNumber,
            chapterDetail,
            chapterDetail.Source.Url);
    }

    private TrackMetadata CreateTrackMetadata(
        int scheduleId,
        BibleReadingSchedule bibleReadingSchedule,
        int sectionNumber,
        int chapterNumber,
        int remainingChapters,
        AlarmSchedule schedule,
        ref bool markedSeekTrack)
    {
        var trackMetadata = new TrackMetadata
        {
            ScheduleId = scheduleId,
            PublicationCode = bibleReadingSchedule.PublicationCode,
            LanguageCode = bibleReadingSchedule.LanguageCode,
            // LookUpPath is now computed from LanguageCode, PublicationCode, SectionNumber, ChapterNumber
            SectionNumber = sectionNumber,
            ChapterNumber = chapterNumber,
            IsLastTrack = remainingChapters == 1
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

    public async Task<ChapterInfo> GetNextChapterInfo(
        BibleReadingSchedule bibleReadingSchedule,
        int currentSectionNumber,
        int currentChapterNumber,
        Func<string, string, int, int, Task<KeyValuePair<BibleSection, BibleChapter>>> getNextBibleChapterAsync)
    {
        var next = await getNextBibleChapterAsync(
            bibleReadingSchedule.LanguageCode,
            bibleReadingSchedule.PublicationCode,
            currentSectionNumber,
            currentChapterNumber);

        if (next.Key == null || next.Value == null)
        {
            throw new InvalidOperationException("Next chapter Key or Value is null");
        }

        if (next.Value.Source == null)
        {
            throw new InvalidOperationException("Next chapter Source is null");
        }

        return new ChapterInfo(
            next.Key.Number,
            next.Value,
            next.Value.Source.Url);
    }
}
