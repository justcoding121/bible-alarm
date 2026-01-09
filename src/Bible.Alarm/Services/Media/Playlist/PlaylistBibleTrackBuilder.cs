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

    public record ChapterInfo(int BookNumber, BibleChapter Chapter, string Url, string LookUpPath);

    public async Task<List<PlayItem>> BuildBibleTracks(
        int scheduleId,
        AlarmSchedule schedule,
        BibleReadingSchedule bibleReadingSchedule,
        Func<string, string, int, int, Task<KeyValuePair<BibleBook, BibleChapter>>> getNextBibleChapterAsync)
    {
        var initialChapterInfo = await GetInitialChapterInfo(bibleReadingSchedule);
        var result = new List<PlayItem>();
        var numberOfChaptersToRead = schedule.NumberOfChaptersToRead;
        var markedSeekTrack = false;

        var currentBookNumber = initialChapterInfo.BookNumber;
        var currentChapter = initialChapterInfo.Chapter;
        var currentUrl = initialChapterInfo.Url;
        var currentLookUpPath = initialChapterInfo.LookUpPath;

        while (numberOfChaptersToRead > 0)
        {
            var trackMetadata = CreateTrackMetadata(
                scheduleId,
                bibleReadingSchedule,
                currentBookNumber,
                currentChapter.Number,
                currentLookUpPath,
                numberOfChaptersToRead,
                schedule,
                ref markedSeekTrack);

            result.Add(new PlayItem(trackMetadata, currentUrl));

            numberOfChaptersToRead--;
            if (numberOfChaptersToRead > 0)
            {
                var next = await GetNextChapterInfo(bibleReadingSchedule, currentBookNumber, currentChapter.Number, getNextBibleChapterAsync);
                currentBookNumber = next.BookNumber;
                currentChapter = next.Chapter;
                currentUrl = next.Url;
                currentLookUpPath = next.LookUpPath;
            }
        }

        return result;
    }

    public async Task<ChapterInfo> GetInitialChapterInfo(BibleReadingSchedule bibleReadingSchedule)
    {
        var chapters = await mediaService.GetBibleChapters(
            bibleReadingSchedule.LanguageCode,
            bibleReadingSchedule.PublicationCode,
            bibleReadingSchedule.BookNumber);

        if (!chapters.TryGetValue(bibleReadingSchedule.ChapterNumber, out var chapterDetail))
        {
            logger.Error(
                $"Chapter: ${bibleReadingSchedule.ChapterNumber}, book: {bibleReadingSchedule.BookNumber}, language: {bibleReadingSchedule.LanguageCode}, pub code: {bibleReadingSchedule.PublicationCode} not in lookup.");
            throw new InvalidOperationException($"Chapter {bibleReadingSchedule.ChapterNumber} not found in book {bibleReadingSchedule.BookNumber}");
        }

        if (chapterDetail.Source == null)
        {
            throw new InvalidOperationException($"Chapter {bibleReadingSchedule.ChapterNumber} Source is null in book {bibleReadingSchedule.BookNumber}");
        }

        return new ChapterInfo(
            bibleReadingSchedule.BookNumber,
            chapterDetail,
            chapterDetail.Source.Url,
            chapterDetail.Source.LookUpPath);
    }

    private TrackMetadata CreateTrackMetadata(
        int scheduleId,
        BibleReadingSchedule bibleReadingSchedule,
        int bookNumber,
        int chapterNumber,
        string lookUpPath,
        int remainingChapters,
        AlarmSchedule schedule,
        ref bool markedSeekTrack)
    {
        var trackMetadata = new TrackMetadata
        {
            ScheduleId = scheduleId,
            PublicationCode = bibleReadingSchedule.PublicationCode,
            LanguageCode = bibleReadingSchedule.LanguageCode,
            LookUpPath = lookUpPath,
            BookNumber = bookNumber,
            ChapterNumber = chapterNumber,
            IsLastTrack = remainingChapters == 1
        };

        var shouldSet = ShouldSetFinishedDuration(markedSeekTrack, schedule, bibleReadingSchedule, trackMetadata);
        logger.Debug("[PlaylistBuild] ShouldSetFinishedDuration: {ShouldSet}, markedSeekTrack: {MarkedSeekTrack}, AlwaysPlayFromStart: {AlwaysPlayFromStart}, ScheduleFinishedDuration: {ScheduleFinishedDuration}, BookMatch: {BookMatch}",
            shouldSet,
            markedSeekTrack,
            schedule.AlwaysPlayFromStart,
            bibleReadingSchedule.FinishedDuration,
            bibleReadingSchedule.BookNumber == trackMetadata.BookNumber);
            
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
               bibleReadingSchedule.BookNumber == trackMetadata.BookNumber;
    }

    public async Task<ChapterInfo> GetNextChapterInfo(
        BibleReadingSchedule bibleReadingSchedule,
        int currentBookNumber,
        int currentChapterNumber,
        Func<string, string, int, int, Task<KeyValuePair<BibleBook, BibleChapter>>> getNextBibleChapterAsync)
    {
        var next = await getNextBibleChapterAsync(
            bibleReadingSchedule.LanguageCode,
            bibleReadingSchedule.PublicationCode,
            currentBookNumber,
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
            next.Value.Source.Url,
            next.Value.Source.LookUpPath);
    }
}

