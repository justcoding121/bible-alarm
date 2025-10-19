using Bible.Alarm.Common.Mvvm;
using Bible.Alarm.Models;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Bible;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Services;

public class PlaylistService(
    ScheduleDbContext scheduleDbContext,
    MediaDbContext mediaDbContext,
    MediaService mediaService)
    : IPlaylistService
{
    private static readonly ILogger Logger = Log.ForContext<PlaylistService>();

    public async Task<long> GetRelavantScheduleToPlay()
    {
        var lastSchedule =
            await scheduleDbContext.GeneralSettings.FirstOrDefaultAsync(x => x.Key == AppConstants.GeneralSettingsKeys.LastPlayedScheduleId);

        AlarmSchedule schedule = null;

        if (!string.IsNullOrEmpty(lastSchedule?.Value))
            await scheduleDbContext.AlarmSchedules.FirstOrDefaultAsync(x => x.Id == long.Parse(lastSchedule.Value));

        if (schedule == null) schedule = await scheduleDbContext.AlarmSchedules.FirstOrDefaultAsync();

        if (schedule == null)
        {
            schedule = await AlarmSchedule.GetSampleSchedule(false, mediaDbContext);
            scheduleDbContext.Add(schedule);
            await scheduleDbContext.SaveChangesAsync();
        }

        return schedule.Id;
    }

    public async Task SaveLastPlayed(long scheduleId)
    {
        var lastSchedule =
            await scheduleDbContext.GeneralSettings.FirstOrDefaultAsync(x => x.Key == AppConstants.GeneralSettingsKeys.LastPlayedScheduleId);

        if (lastSchedule == null)
        {
            lastSchedule = new GeneralSettings { Key = AppConstants.GeneralSettingsKeys.LastPlayedScheduleId };
            scheduleDbContext.GeneralSettings.Add(lastSchedule);
        }

        lastSchedule.Value = scheduleId.ToString();
        await scheduleDbContext.SaveChangesAsync();
    }

    public async Task MarkTrackAsPlayed(NotificationDetail trackDetail)
    {
        var trackChanged = false;

        var schedule = await scheduleDbContext.AlarmSchedules
            .Include(x => x.Music)
            .Include(x => x.BibleReadingSchedule)
            .FirstAsync(x => x.Id == trackDetail.ScheduleId);

        if (trackDetail.PlayType == PlayType.Music)
        {
            if (!schedule.Music.Repeat)
            {
                schedule.Music.TrackNumber = trackDetail.TrackNumber;
                var next = await NextMusicUrlToPlay(schedule, true);
                schedule.Music.TrackNumber = next.PlayDetail.TrackNumber;
            }
        }
        else
        {
            var bibleReadingSchedule = schedule.BibleReadingSchedule;

            if (bibleReadingSchedule.BookNumber != trackDetail.BookNumber
                || bibleReadingSchedule.ChapterNumber != trackDetail.ChapterNumber)
                trackChanged = true;

            bibleReadingSchedule.BookNumber = trackDetail.BookNumber;
            bibleReadingSchedule.ChapterNumber = trackDetail.ChapterNumber;
            bibleReadingSchedule.FinishedDuration = trackDetail.FinishedDuration;
        }

        await scheduleDbContext.SaveChangesAsync();

        if (trackChanged) Messenger<object>.Publish(MvvmMessages.TrackChanged, schedule.Id);
    }

    public async Task MarkTrackAsFinished(NotificationDetail trackDetail)
    {
        var schedule = await scheduleDbContext.AlarmSchedules
            .Include(x => x.Music)
            .Include(x => x.BibleReadingSchedule)
            .FirstAsync(x => x.Id == trackDetail.ScheduleId);

        if (trackDetail.PlayType == PlayType.Music)
        {
            if (!schedule.Music.Repeat)
            {
                var next = await NextMusicUrlToPlay(schedule, true);
                schedule.Music.TrackNumber = next.PlayDetail.TrackNumber;
            }
        }
        else
        {
            var bibleReadingSchedule = schedule.BibleReadingSchedule;

            var next = await GetNextBibleChapter(trackDetail.LanguageCode, trackDetail.PublicationCode,
                trackDetail.BookNumber, trackDetail.ChapterNumber);

            bibleReadingSchedule.BookNumber = next.Key.Number;
            bibleReadingSchedule.ChapterNumber = next.Value.Number;
            bibleReadingSchedule.FinishedDuration = default;
        }

        await scheduleDbContext.SaveChangesAsync();

        Messenger<object>.Publish(MvvmMessages.TrackChanged, schedule.Id);
    }

    public async Task<PlayItem> NextTrack(long scheduleId)
    {
        var schedule = await scheduleDbContext.AlarmSchedules
            .AsNoTracking()
            .Include(x => x.Music)
            .Include(x => x.BibleReadingSchedule)
            .FirstOrDefaultAsync(x => x.Id == scheduleId);

        if (schedule == null) throw new ArgumentException($"Invalid schedule Id {scheduleId}");

        if (schedule.MusicEnabled) return await NextMusicUrlToPlay(schedule);

        var bibleReadingSchedule = schedule.BibleReadingSchedule;

        var bookNumber = bibleReadingSchedule.BookNumber;
        var chapter = bibleReadingSchedule.ChapterNumber;

        var chapterDetail = await mediaService.GetBibleChapter(bibleReadingSchedule.LanguageCode,
            bibleReadingSchedule.PublicationCode, bookNumber, chapter);

        if (chapterDetail == null)
            Logger.Error(
                $"Chapter: ${chapter}, book: {bookNumber}, language: {bibleReadingSchedule.LanguageCode}, pub code: {bibleReadingSchedule.PublicationCode} not in lookup. ");

        var publicationCode = bibleReadingSchedule.PublicationCode;
        var languageCode = bibleReadingSchedule.LanguageCode;
        var url = chapterDetail.Url;
        var lookUpPath = chapterDetail.LookUpPath;

        var notificationDetail = new NotificationDetail
        {
            ScheduleId = scheduleId,
            PublicationCode = publicationCode,
            LanguageCode = languageCode,
            LookUpPath = lookUpPath,
            BookNumber = bookNumber,
            ChapterNumber = chapter,
            IsLastTrack = false
        };

        return new PlayItem(notificationDetail, url);
    }

    public async Task<List<PlayItem>> NextTracks(long scheduleId)
    {
        var result = new List<PlayItem>();

        var schedule = await scheduleDbContext.AlarmSchedules
            .AsNoTracking()
            .Include(x => x.Music)
            .Include(x => x.BibleReadingSchedule)
            .FirstOrDefaultAsync(x => x.Id == scheduleId);

        if (schedule == null) throw new ArgumentException($"Invalid schedule Id {scheduleId}");

        var numberOfChaptersToRead = schedule.NumberOfChaptersToRead;

        if (schedule.MusicEnabled) result.Add(await NextMusicUrlToPlay(schedule));

        var bibleReadingSchedule = schedule.BibleReadingSchedule;

        var bookNumber = bibleReadingSchedule.BookNumber;
        var chapter = bibleReadingSchedule.ChapterNumber;
        var chapters = await mediaService.GetBibleChapters(bibleReadingSchedule.LanguageCode,
            bibleReadingSchedule.PublicationCode, bookNumber);

        if (!chapters.ContainsKey(chapter))
            Logger.Error(
                $"Chapter: ${chapter}, book: {bookNumber}, language: {bibleReadingSchedule.LanguageCode}, pub code: {bibleReadingSchedule.PublicationCode} not in lookup. ");

        var chapterDetail = chapters[chapter];

        var publicationCode = bibleReadingSchedule.PublicationCode;
        var languageCode = bibleReadingSchedule.LanguageCode;
        var url = chapterDetail.Url;
        var lookUpPath = chapterDetail.LookUpPath;

        var markedSeekTrack = false;

        while (numberOfChaptersToRead > 0)
        {
            var notificationDetail = new NotificationDetail
            {
                ScheduleId = scheduleId,
                PublicationCode = publicationCode,
                LanguageCode = languageCode,
                LookUpPath = lookUpPath,
                BookNumber = bookNumber,
                ChapterNumber = chapter,
                IsLastTrack = numberOfChaptersToRead == 1 ? true : false
            };

            //resume from where it was stopped last time
            if (!markedSeekTrack
                && !schedule.AlwaysPlayFromStart
                && !bibleReadingSchedule.FinishedDuration.Equals(default)
                && bibleReadingSchedule.LanguageCode == notificationDetail.LanguageCode
                && bibleReadingSchedule.PublicationCode == notificationDetail.PublicationCode
                && bookNumber == notificationDetail.BookNumber)
                notificationDetail.FinishedDuration = bibleReadingSchedule.FinishedDuration;

            markedSeekTrack = true;

            result.Add(new PlayItem(notificationDetail, url));

            numberOfChaptersToRead--;

            var next = await GetNextBibleChapter(bibleReadingSchedule.LanguageCode,
                bibleReadingSchedule.PublicationCode,
                bookNumber, chapter);

            bookNumber = next.Key.Number;
            chapter = next.Value.Number;
            url = next.Value.Url;
            lookUpPath = next.Value.LookUpPath;
        }

        return result;
    }

    public async Task MoveToNextBibleChapter(long scheduleId)
    {
        var schedule = await scheduleDbContext.AlarmSchedules
            .Include(x => x.BibleReadingSchedule)
            .FirstOrDefaultAsync(x => x.Id == scheduleId);

        if (schedule == null) throw new ArgumentException($"Invalid schedule Id {scheduleId}");

        var bibleReadingSchedule = schedule.BibleReadingSchedule;

        var bookNumber = bibleReadingSchedule.BookNumber;
        var chapter = bibleReadingSchedule.ChapterNumber;
        var publicationCode = bibleReadingSchedule.PublicationCode;
        var languageCode = bibleReadingSchedule.LanguageCode;

        var next = await GetNextBibleChapter(languageCode, publicationCode, bookNumber, chapter);

        bibleReadingSchedule.BookNumber = next.Key.Number;
        bibleReadingSchedule.ChapterNumber = next.Value.Number;
        bibleReadingSchedule.FinishedDuration = default;

        await scheduleDbContext.SaveChangesAsync();
    }

    public async Task MoveToPreviousBibleChapter(long scheduleId)
    {
        var schedule = await scheduleDbContext.AlarmSchedules
            .Include(x => x.BibleReadingSchedule)
            .FirstOrDefaultAsync(x => x.Id == scheduleId);

        if (schedule == null) throw new ArgumentException($"Invalid schedule Id {scheduleId}");

        var bibleReadingSchedule = schedule.BibleReadingSchedule;

        var bookNumber = bibleReadingSchedule.BookNumber;
        var chapter = bibleReadingSchedule.ChapterNumber;
        var publicationCode = bibleReadingSchedule.PublicationCode;
        var languageCode = bibleReadingSchedule.LanguageCode;

        var previous = await GetPreviousBibleChapter(languageCode, publicationCode, bookNumber, chapter);

        bibleReadingSchedule.BookNumber = previous.Key.Number;
        bibleReadingSchedule.ChapterNumber = previous.Value.Number;
        bibleReadingSchedule.FinishedDuration = default;

        await scheduleDbContext.SaveChangesAsync();
    }

    public async Task<KeyValuePair<BibleBook, BibleChapter>> GetNextBibleChapter(string languageCode,
        string publicationCode, int bookNumber, int chapter)
    {
        var currentBook = await mediaService.GetBibleBook(languageCode, publicationCode, bookNumber);
        var chapters = await mediaService.GetBibleChapters(languageCode, publicationCode, bookNumber);
        var nextChapter = chapters.NextHigher(chapter);

        if (!nextChapter.Equals(default(KeyValuePair<int, BibleChapter>)))
            return new KeyValuePair<BibleBook, BibleChapter>(currentBook, nextChapter.Value);

        var nextBook = await GetNextBibleBook(languageCode, publicationCode, bookNumber);

        chapters = await mediaService.GetBibleChapters(languageCode, publicationCode, nextBook.Key);
        return new KeyValuePair<BibleBook, BibleChapter>(nextBook.Value, chapters[1]);
    }

    public async Task<KeyValuePair<BibleBook, BibleChapter>> GetPreviousBibleChapter(string languageCode,
        string publicationCode, int bookNumber, int chapter)
    {
        var currentBook = await mediaService.GetBibleBook(languageCode, publicationCode, bookNumber);
        var chapters = await mediaService.GetBibleChapters(languageCode, publicationCode, bookNumber);
        var previousChapter = chapters.NextLower(chapter);

        if (!previousChapter.Equals(default(KeyValuePair<int, BibleChapter>)))
            return new KeyValuePair<BibleBook, BibleChapter>(currentBook, previousChapter.Value);

        var previousBook = await GetPreviousBibleBook(languageCode, publicationCode, bookNumber);

        chapters = await mediaService.GetBibleChapters(languageCode, publicationCode, previousBook.Key);
        return new KeyValuePair<BibleBook, BibleChapter>(previousBook.Value, chapters[chapters.Count]);
    }

    public async Task<KeyValuePair<int, BibleBook>> GetPreviousBibleBook(string languageCode, string publicationCode,
        int bookNumber)
    {
        var books = await mediaService.GetBibleBooks(languageCode, publicationCode);
        var previousBook = books.NextLower(bookNumber);

        if (!previousBook.Equals(default(KeyValuePair<int, BibleBook>))) return previousBook;

        return books.Max();
    }

    public async Task<KeyValuePair<int, BibleBook>> GetNextBibleBook(string languageCode, string publicationCode,
        int bookNumber)
    {
        var books = await mediaService.GetBibleBooks(languageCode, publicationCode);
        var previousBook = books.NextHigher(bookNumber);

        if (!previousBook.Equals(default(KeyValuePair<int, BibleBook>))) return previousBook;

        return books.Min();
    }

    private async Task<PlayItem> NextMusicUrlToPlay(AlarmSchedule schedule, bool next = false)
    {
        switch (schedule.Music.MusicType)
        {
            case MusicType.Melodies:
                var melodyMusic = schedule.Music;
                var melodyTracks = await mediaService.GetMelodyMusicTracks(melodyMusic.PublicationCode);

                var melodyTrack =
                    melodyTracks[next ? melodyMusic.TrackNumber % melodyTracks.Count + 1 : melodyMusic.TrackNumber];
                return new PlayItem(new NotificationDetail
                {
                    ScheduleId = schedule.Id,
                    PublicationCode = melodyMusic.PublicationCode,
                    TrackNumber = melodyTrack.Number,
                    LookUpPath = melodyTrack.LookUpPath
                }, melodyTrack.Url);

            case MusicType.Vocals:
                var vocalMusic = schedule.Music;
                var vocalTracks =
                    await mediaService.GetVocalMusicTracks(vocalMusic.LanguageCode, vocalMusic.PublicationCode);
                var vocalTrack =
                    vocalTracks[next ? vocalMusic.TrackNumber % vocalTracks.Count + 1 : vocalMusic.TrackNumber];
                return new PlayItem(new NotificationDetail
                {
                    ScheduleId = schedule.Id,
                    PublicationCode = vocalMusic.PublicationCode,
                    LanguageCode = vocalMusic.LanguageCode,
                    TrackNumber = vocalTrack.Number,
                    LookUpPath = vocalTrack.LookUpPath
                }, vocalTrack.Url);

            default:
                throw new ApplicationException("Invalid MusicType.");
        }
    }

    public void Dispose()
    {
        mediaDbContext.Dispose();
        scheduleDbContext.Dispose();
        mediaService.Dispose();
    }
}