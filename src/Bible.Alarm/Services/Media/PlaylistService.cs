using System.Linq;
using Bible.Alarm.Common.Infrastructure.Schedule;
using CommunityToolkit.Mvvm.Messaging;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Models;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Infrastructure.Schedule;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Infrastructure;
using Bible.Alarm.Shared.Models.Media.Bible;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class PlaylistService(
    ILogger logger,
    IServiceScopeFactory scopeFactory,
    MediaService mediaService)
    : IPlaylistService
{
    private readonly ILogger _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    public async Task<long> GetRelavantScheduleToPlay()
    {
        using var scope = _scopeFactory.CreateScope();
        var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
        var mediaDbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        
        var lastSchedule =
            await scheduleDbContext.GeneralSettings.FirstOrDefaultAsync(x => x.Key == AppConstants.GeneralSettingsKeys.LastPlayedScheduleId);

        AlarmSchedule schedule = null;

        if (!string.IsNullOrEmpty(lastSchedule?.Value))
            schedule = await scheduleDbContext.AlarmSchedules.FirstOrDefaultAsync(x => x.Id == long.Parse(lastSchedule.Value));

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
        using var scope = _scopeFactory.CreateScope();
        var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
        
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
        using var scope = _scopeFactory.CreateScope();
        var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
        
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

        if (trackChanged) WeakReferenceMessenger.Default.Send(new TrackChangedMessage((int)schedule.Id));
    }

    public async Task MarkTrackAsFinished(NotificationDetail trackDetail)
    {
        using var scope = _scopeFactory.CreateScope();
        var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
        
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
            bibleReadingSchedule.FinishedDuration = TimeSpan.Zero;
        }

        await scheduleDbContext.SaveChangesAsync();

        WeakReferenceMessenger.Default.Send(new TrackChangedMessage((int)schedule.Id));
    }

    public async Task<PlayItem> NextTrack(long scheduleId)
    {
        using var scope = _scopeFactory.CreateScope();
        var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
        
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
            _logger.Error(
                $"Chapter: ${chapter}, book: {bookNumber}, language: {bibleReadingSchedule.LanguageCode}, pub code: {bibleReadingSchedule.PublicationCode} not in lookup. ");

        var publicationCode = bibleReadingSchedule.PublicationCode;
        var languageCode = bibleReadingSchedule.LanguageCode;
        var url = chapterDetail.Source.Url;
        var lookUpPath = chapterDetail.Source.LookUpPath;

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
        using var scope = _scopeFactory.CreateScope();
        var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
        
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
            _logger.Error(
                $"Chapter: ${chapter}, book: {bookNumber}, language: {bibleReadingSchedule.LanguageCode}, pub code: {bibleReadingSchedule.PublicationCode} not in lookup. ");

        var chapterDetail = chapters[chapter];

        var publicationCode = bibleReadingSchedule.PublicationCode;
        var languageCode = bibleReadingSchedule.LanguageCode;
        var url = chapterDetail.Source.Url;
        var lookUpPath = chapterDetail.Source.LookUpPath;

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
                && !bibleReadingSchedule.FinishedDuration.Equals(TimeSpan.Zero)
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
            url = next.Value.Source.Url;
            lookUpPath = next.Value.Source.LookUpPath;
        }

        return result;
    }

    public async Task MoveToNextBibleChapter(long scheduleId)
    {
        using var scope = _scopeFactory.CreateScope();
        var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
        
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
        bibleReadingSchedule.FinishedDuration = TimeSpan.Zero;

        await scheduleDbContext.SaveChangesAsync();
    }

    public async Task MoveToPreviousBibleChapter(long scheduleId)
    {
        using var scope = _scopeFactory.CreateScope();
        var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
        
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
        bibleReadingSchedule.FinishedDuration = TimeSpan.Zero;

        await scheduleDbContext.SaveChangesAsync();
    }

    public async Task<KeyValuePair<BibleBook, BibleChapter>> GetNextBibleChapter(string languageCode,
        string publicationCode, int bookNumber, int chapter)
    {
        var currentBook = await mediaService.GetBibleBook(languageCode, publicationCode, bookNumber);
        var chapters = await mediaService.GetBibleChapters(languageCode, publicationCode, bookNumber);
        var nextChapter = chapters.SkipWhile(kvp => kvp.Key <= chapter).FirstOrDefault();

        if (!nextChapter.Equals(default(KeyValuePair<int, BibleChapter>)))
            return new KeyValuePair<BibleBook, BibleChapter>(currentBook, nextChapter.Value);

        var nextBook = await GetNextBibleBook(languageCode, publicationCode, bookNumber);

        chapters = await mediaService.GetBibleChapters(languageCode, publicationCode, nextBook.Key);
        return new KeyValuePair<BibleBook, BibleChapter>(nextBook.Value, chapters.ElementAt(1).Value);
    }

    public async Task<KeyValuePair<BibleBook, BibleChapter>> GetPreviousBibleChapter(string languageCode,
        string publicationCode, int bookNumber, int chapter)
    {
        var currentBook = await mediaService.GetBibleBook(languageCode, publicationCode, bookNumber);
        var chapters = await mediaService.GetBibleChapters(languageCode, publicationCode, bookNumber);
        var previousChapter = chapters.Reverse().SkipWhile(kvp => kvp.Key >= chapter).FirstOrDefault();

        if (!previousChapter.Equals(default(KeyValuePair<int, BibleChapter>)))
            return new KeyValuePair<BibleBook, BibleChapter>(currentBook, previousChapter.Value);

        var previousBook = await GetPreviousBibleBook(languageCode, publicationCode, bookNumber);

        chapters = await mediaService.GetBibleChapters(languageCode, publicationCode, previousBook.Key);
        return new KeyValuePair<BibleBook, BibleChapter>(previousBook.Value, chapters.ElementAt(chapters.Count - 1).Value);
    }

    public async Task<KeyValuePair<int, BibleBook>> GetPreviousBibleBook(string languageCode, string publicationCode,
        int bookNumber)
    {
        var books = await mediaService.GetBibleBooks(languageCode, publicationCode);
        var previousBook = books.Reverse().SkipWhile(kvp => kvp.Key >= bookNumber).FirstOrDefault();

        if (!previousBook.Equals(default(KeyValuePair<int, BibleBook>))) return previousBook;

        var maxKey = books.Keys.Max();
        return new KeyValuePair<int, BibleBook>(maxKey, books[maxKey]);
    }

    public async Task<KeyValuePair<int, BibleBook>> GetNextBibleBook(string languageCode, string publicationCode,
        int bookNumber)
    {
        var books = await mediaService.GetBibleBooks(languageCode, publicationCode);
        var previousBook = books.SkipWhile(kvp => kvp.Key <= bookNumber).FirstOrDefault();

        if (!previousBook.Equals(default(KeyValuePair<int, BibleBook>))) return previousBook;

        var minKey = books.Keys.Min();
        return new KeyValuePair<int, BibleBook>(minKey, books[minKey]);
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
                    LookUpPath = melodyTrack.Source.LookUpPath
                }, melodyTrack.Source.Url);

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
                    LookUpPath = vocalTrack.Source.LookUpPath
                }, vocalTrack.Source.Url);

            default:
                throw new ApplicationException("Invalid MusicType.");
        }
    }

    public void Dispose()
    {
        // Note: DbContext instances are now created via IServiceScopeFactory and disposed by the scope
        // mediaService (MediaService) is a singleton and should not be disposed here
        // as it is managed by the DI container
    }
}