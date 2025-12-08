using Bible.Alarm.Common;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Models;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Stores.Actions.Schedule;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Microsoft.EntityFrameworkCore;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Media;

public class PlaylistService(
    ILogger logger,
    IServiceScopeFactory scopeFactory,
    IMediaService mediaService,
    IDispatcher dispatcher)
    : IPlaylistService, IDisposable
{
    private readonly ILogger _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private bool _isDisposed;

    public async Task<int> GetRelevantScheduleToPlay()
    {
        using var scope = _scopeFactory.CreateScope();
        var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
        var mediaDbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        
        // Query directly by Key - unique index ensures efficient lookup
        var lastSchedule = await scheduleDbContext.GeneralSettings
            .FirstOrDefaultAsync(x => x.Key == AppConstants.GeneralSettingsKeys.LastPlayedScheduleId, _cancellationTokenSource.Token);

        AlarmSchedule schedule = null;

        if (!string.IsNullOrEmpty(lastSchedule?.Value))
            schedule = await scheduleDbContext.AlarmSchedules.FirstOrDefaultAsync(x => x.Id == long.Parse(lastSchedule.Value), _cancellationTokenSource.Token);

        if (schedule == null) schedule = await scheduleDbContext.AlarmSchedules.FirstOrDefaultAsync(_cancellationTokenSource.Token);

        if (schedule == null)
        {
            schedule = await AlarmSchedule.GetSampleSchedule(false, mediaDbContext);
            scheduleDbContext.Add(schedule);
            await scheduleDbContext.SaveChangesAsync(_cancellationTokenSource.Token);
        }

        return schedule.Id;
    }

    public async Task SaveLastPlayed(int scheduleId)
    {
        using var scope = _scopeFactory.CreateScope();
        var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
        
        // Query directly by Key - unique index ensures efficient lookup
        var lastSchedule = await scheduleDbContext.GeneralSettings
            .FirstOrDefaultAsync(x => x.Key == AppConstants.GeneralSettingsKeys.LastPlayedScheduleId, _cancellationTokenSource.Token);

        if (lastSchedule == null)
        {
            lastSchedule = new GeneralSettings { Key = AppConstants.GeneralSettingsKeys.LastPlayedScheduleId };
            scheduleDbContext.GeneralSettings.Add(lastSchedule);
        }

        lastSchedule.Value = scheduleId.ToString();
        await scheduleDbContext.SaveChangesAsync(_cancellationTokenSource.Token);
    }

    public async Task MarkTrackAsPlayed(TrackMetadata trackMetadata)
    {
        using var scope = _scopeFactory.CreateScope();
        var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
        
        var trackChanged = false;

        var schedule = await scheduleDbContext.AlarmSchedules
            .Include(x => x.Music)
            .Include(x => x.BibleReadingSchedule)
            .FirstAsync(x => x.Id == trackMetadata.ScheduleId, _cancellationTokenSource.Token);

        if (trackMetadata.PlayType == PlayType.Music)
        {
            if (schedule.Music != null && !schedule.Music.Repeat)
            {
                schedule.Music.TrackNumber = trackMetadata.TrackNumber;
                var next = await NextMusicUrlToPlay(schedule, true);
                schedule.Music.TrackNumber = next.Metadata.TrackNumber;
            }
        }
        else
        {
            var bibleReadingSchedule = schedule.BibleReadingSchedule;
            if (bibleReadingSchedule == null)
                throw new InvalidOperationException($"BibleReadingSchedule is null for schedule {schedule.Id}");

            // Check if chapter changed (not just progress)
            if (bibleReadingSchedule.BookNumber != trackMetadata.BookNumber
                || bibleReadingSchedule.ChapterNumber != trackMetadata.ChapterNumber)
                trackChanged = true;

            bibleReadingSchedule.BookNumber = trackMetadata.BookNumber;
            bibleReadingSchedule.ChapterNumber = trackMetadata.ChapterNumber;
            bibleReadingSchedule.FinishedDuration = trackMetadata.FinishedDuration;
        }

        await scheduleDbContext.SaveChangesAsync(_cancellationTokenSource.Token);

        if (trackChanged)
        {
            // Only update state when chapter/track actually changed (not just progress)
            // Reload the schedule with all includes to get the updated data
            var updatedSchedule = await scheduleDbContext.AlarmSchedules
                .Include(x => x.BibleReadingSchedule)
                .Include(x => x.Music)
                .FirstAsync(x => x.Id == trackMetadata.ScheduleId, _cancellationTokenSource.Token);
            
            // Update the Fluxor store to trigger state change and UI refresh
            // ScheduleListItem now subscribes to ApplicationState changes instead of TrackChangedMessage
            _dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
        }
    }

    public async Task MarkTrackAsFinished(TrackMetadata trackMetadata)
    {
        AlarmSchedule updatedSchedule = null;
        
        using (var scope = _scopeFactory.CreateScope())
        {
            var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
            
            var schedule = await scheduleDbContext.AlarmSchedules
                .Include(x => x.Music)
                .Include(x => x.BibleReadingSchedule)
                .FirstAsync(x => x.Id == trackMetadata.ScheduleId, _cancellationTokenSource.Token);

            if (trackMetadata.PlayType == PlayType.Music)
            {
                if (schedule.Music != null && !schedule.Music.Repeat)
                {
                    var next = await NextMusicUrlToPlay(schedule, true);
                    schedule.Music.TrackNumber = next.Metadata.TrackNumber;
                }
            }
            else
            {
                var bibleReadingSchedule = schedule.BibleReadingSchedule;
                if (bibleReadingSchedule == null)
                    throw new InvalidOperationException($"BibleReadingSchedule is null for schedule {schedule.Id}");

                var next = await GetNextBibleChapter(trackMetadata.LanguageCode, trackMetadata.PublicationCode,
                    trackMetadata.BookNumber, trackMetadata.ChapterNumber);

                if (next.Key == null || next.Value == null)
                    throw new InvalidOperationException($"Next chapter Key or Value is null");
                
                bibleReadingSchedule.BookNumber = next.Key.Number;
                bibleReadingSchedule.ChapterNumber = next.Value.Number;
                bibleReadingSchedule.FinishedDuration = TimeSpan.Zero;
            }

            await scheduleDbContext.SaveChangesAsync(_cancellationTokenSource.Token);
            
            // Reload the schedule with all includes to get the updated data
            updatedSchedule = await scheduleDbContext.AlarmSchedules
                .Include(x => x.BibleReadingSchedule)
                .Include(x => x.Music)
                .FirstAsync(x => x.Id == trackMetadata.ScheduleId, _cancellationTokenSource.Token);
        }
        
        // Update the Fluxor store to trigger state change and UI refresh
        // ScheduleListItem now subscribes to ApplicationState changes instead of TrackChangedMessage
        if (updatedSchedule != null)
        {
            _dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
        }
    }

    public async Task<PlayItem> NextTrack(int scheduleId)
    {
        using var scope = _scopeFactory.CreateScope();
        var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
        
        var schedule = await scheduleDbContext.AlarmSchedules
            .AsNoTracking()
            .Include(x => x.Music)
            .Include(x => x.BibleReadingSchedule)
            .FirstOrDefaultAsync(x => x.Id == scheduleId, _cancellationTokenSource.Token);

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

        var trackMetadata = new TrackMetadata
        {
            ScheduleId = scheduleId,
            PublicationCode = publicationCode,
            LanguageCode = languageCode,
            LookUpPath = lookUpPath,
            BookNumber = bookNumber,
            ChapterNumber = chapter,
            IsLastTrack = false
        };

        return new PlayItem(trackMetadata, url);
    }

    public async Task<List<PlayItem>> NextTracks(int scheduleId)
    {
        using var scope = _scopeFactory.CreateScope();
        var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
        
        var result = new List<PlayItem>();

        var schedule = await scheduleDbContext.AlarmSchedules
            .AsNoTracking()
            .Include(x => x.Music)
            .Include(x => x.BibleReadingSchedule)
            .FirstOrDefaultAsync(x => x.Id == scheduleId, _cancellationTokenSource.Token);

        if (schedule == null) throw new ArgumentException($"Invalid schedule Id {scheduleId}");

        var numberOfChaptersToRead = schedule.NumberOfChaptersToRead;

        if (schedule.MusicEnabled) result.Add(await NextMusicUrlToPlay(schedule));

        var bibleReadingSchedule = schedule.BibleReadingSchedule;
        if (bibleReadingSchedule == null)
            throw new InvalidOperationException($"BibleReadingSchedule is null for schedule {scheduleId}");

        var bookNumber = bibleReadingSchedule.BookNumber;
        var chapter = bibleReadingSchedule.ChapterNumber;
        var chapters = await mediaService.GetBibleChapters(bibleReadingSchedule.LanguageCode,
            bibleReadingSchedule.PublicationCode, bookNumber);

        if (!chapters.ContainsKey(chapter))
        {
            _logger.Error(
                $"Chapter: ${chapter}, book: {bookNumber}, language: {bibleReadingSchedule.LanguageCode}, pub code: {bibleReadingSchedule.PublicationCode} not in lookup. ");
            throw new InvalidOperationException($"Chapter {chapter} not found in book {bookNumber}");
        }

        var chapterDetail = chapters[chapter];
        if (chapterDetail.Source == null)
            throw new InvalidOperationException($"Chapter {chapter} Source is null in book {bookNumber}");

        var publicationCode = bibleReadingSchedule.PublicationCode;
        var languageCode = bibleReadingSchedule.LanguageCode;
        var url = chapterDetail.Source.Url;
        var lookUpPath = chapterDetail.Source.LookUpPath;

        var markedSeekTrack = false;

        while (numberOfChaptersToRead > 0)
        {
            var trackMetadata = new TrackMetadata
            {
                ScheduleId = scheduleId,
                PublicationCode = publicationCode,
                LanguageCode = languageCode,
                LookUpPath = lookUpPath,
                BookNumber = bookNumber,
                ChapterNumber = chapter,
                IsLastTrack = numberOfChaptersToRead == 1
            };

            if (!markedSeekTrack
                && !schedule.AlwaysPlayFromStart
                && !bibleReadingSchedule.FinishedDuration.Equals(TimeSpan.Zero)
                && bibleReadingSchedule.LanguageCode == trackMetadata.LanguageCode
                && bibleReadingSchedule.PublicationCode == trackMetadata.PublicationCode
                && bookNumber == trackMetadata.BookNumber)
                trackMetadata.FinishedDuration = bibleReadingSchedule.FinishedDuration;

            markedSeekTrack = true;

            result.Add(new PlayItem(trackMetadata, url));

            numberOfChaptersToRead--;

            var next = await GetNextBibleChapter(bibleReadingSchedule.LanguageCode,
                bibleReadingSchedule.PublicationCode,
                bookNumber, chapter);

            if (next.Key == null || next.Value == null)
                throw new InvalidOperationException($"Next chapter Key or Value is null");
            if (next.Value.Source == null)
                throw new InvalidOperationException($"Next chapter Source is null");
            
            bookNumber = next.Key.Number;
            chapter = next.Value.Number;
            url = next.Value.Source.Url;
            lookUpPath = next.Value.Source.LookUpPath;
        }

        return result;
    }

    public async Task MoveToNextBibleChapter(int scheduleId)
    {
        AlarmSchedule updatedSchedule = null;
        
        using (var scope = _scopeFactory.CreateScope())
        {
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
            
            // Reload the schedule with all includes to get the updated data
            updatedSchedule = await scheduleDbContext.AlarmSchedules
                .Include(x => x.BibleReadingSchedule)
                .Include(x => x.Music)
                .FirstAsync(x => x.Id == scheduleId);
        }
        
        // Update the Fluxor store to trigger state change and UI refresh
        if (updatedSchedule != null)
        {
            _dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
        }
    }

    public async Task MoveToPreviousBibleChapter(int scheduleId)
    {
        AlarmSchedule updatedSchedule = null;
        
        using (var scope = _scopeFactory.CreateScope())
        {
            var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
            
            var schedule = await scheduleDbContext.AlarmSchedules
                .Include(x => x.BibleReadingSchedule)
                .FirstOrDefaultAsync(x => x.Id == scheduleId, _cancellationTokenSource.Token);

            if (schedule == null) throw new ArgumentException($"Invalid schedule Id {scheduleId}");

            var bibleReadingSchedule = schedule.BibleReadingSchedule;

            var bookNumber = bibleReadingSchedule.BookNumber;
            var chapter = bibleReadingSchedule.ChapterNumber;
            var publicationCode = bibleReadingSchedule.PublicationCode;
            var languageCode = bibleReadingSchedule.LanguageCode;

            var previous = await GetPreviousBibleChapter(languageCode, publicationCode, bookNumber, chapter);

            if (previous.Key == null || previous.Value == null)
                throw new InvalidOperationException($"Previous chapter Key or Value is null");
            
            bibleReadingSchedule.BookNumber = previous.Key.Number;
            bibleReadingSchedule.ChapterNumber = previous.Value.Number;
            bibleReadingSchedule.FinishedDuration = TimeSpan.Zero;

            await scheduleDbContext.SaveChangesAsync(_cancellationTokenSource.Token);
            
            // Reload the schedule with all includes to get the updated data
            updatedSchedule = await scheduleDbContext.AlarmSchedules
                .Include(x => x.BibleReadingSchedule)
                .Include(x => x.Music)
                .FirstAsync(x => x.Id == scheduleId, _cancellationTokenSource.Token);
        }
        
        // Update the Fluxor store to trigger state change and UI refresh
        if (updatedSchedule != null)
        {
            _dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
        }
    }

    public async Task<KeyValuePair<BibleBook, BibleChapter>> GetNextBibleChapter(string languageCode,
        string publicationCode, int bookNumber, int chapter)
    {
        var currentBook = await mediaService.GetBibleBook(languageCode, publicationCode, bookNumber);
        if (currentBook == null)
            throw new InvalidOperationException($"Bible book not found: languageCode={languageCode}, publicationCode={publicationCode}, bookNumber={bookNumber}");
        
        var chapters = await mediaService.GetBibleChapters(languageCode, publicationCode, bookNumber);
        var nextChapter = chapters.SkipWhile(kvp => kvp.Key <= chapter).FirstOrDefault();

        if (!nextChapter.Equals(default(KeyValuePair<int, BibleChapter>)))
            return new KeyValuePair<BibleBook, BibleChapter>(currentBook, nextChapter.Value);

        var nextBook = await GetNextBibleBook(languageCode, publicationCode, bookNumber);
        if (nextBook.Value == null)
            throw new InvalidOperationException($"Next bible book not found: languageCode={languageCode}, publicationCode={publicationCode}, bookNumber={bookNumber}");

        chapters = await mediaService.GetBibleChapters(languageCode, publicationCode, nextBook.Key);
        if (chapters.Count == 0)
            throw new InvalidOperationException($"No chapters in next book: languageCode={languageCode}, publicationCode={publicationCode}, bookNumber={nextBook.Key}");
        
        // Start at the first chapter of the next book (index 0)
        return new KeyValuePair<BibleBook, BibleChapter>(nextBook.Value, chapters.ElementAt(0).Value);
    }

    public async Task<KeyValuePair<BibleBook, BibleChapter>> GetPreviousBibleChapter(string languageCode,
        string publicationCode, int bookNumber, int chapter)
    {
        var currentBook = await mediaService.GetBibleBook(languageCode, publicationCode, bookNumber);
        if (currentBook == null)
            throw new InvalidOperationException($"Bible book not found: languageCode={languageCode}, publicationCode={publicationCode}, bookNumber={bookNumber}");
        
        var chapters = await mediaService.GetBibleChapters(languageCode, publicationCode, bookNumber);
        var previousChapter = chapters.Reverse().SkipWhile(kvp => kvp.Key >= chapter).FirstOrDefault();

        if (!previousChapter.Equals(default(KeyValuePair<int, BibleChapter>)))
            return new KeyValuePair<BibleBook, BibleChapter>(currentBook, previousChapter.Value);

        var previousBook = await GetPreviousBibleBook(languageCode, publicationCode, bookNumber);
        if (previousBook.Value == null)
            throw new InvalidOperationException($"Previous bible book not found: languageCode={languageCode}, publicationCode={publicationCode}, bookNumber={bookNumber}");

        chapters = await mediaService.GetBibleChapters(languageCode, publicationCode, previousBook.Key);
        if (chapters.Count == 0)
            throw new InvalidOperationException($"No chapters in previous book: languageCode={languageCode}, publicationCode={publicationCode}, bookNumber={previousBook.Key}");
        
        return new KeyValuePair<BibleBook, BibleChapter>(previousBook.Value, chapters.ElementAt(chapters.Count - 1).Value);
    }

    public async Task<KeyValuePair<int, BibleBook>> GetPreviousBibleBook(string languageCode, string publicationCode,
        int bookNumber)
    {
        var books = await mediaService.GetBibleBooks(languageCode, publicationCode);
        if (books.Count == 0)
            throw new InvalidOperationException($"No bible books found: languageCode={languageCode}, publicationCode={publicationCode}");
        
        var previousBook = books.Reverse().SkipWhile(kvp => kvp.Key >= bookNumber).FirstOrDefault();

        if (!previousBook.Equals(default(KeyValuePair<int, BibleBook>))) return previousBook;

        var maxKey = books.Keys.Max();
        if (!books.TryGetValue(maxKey, out var maxBook))
            throw new InvalidOperationException($"Bible book with key {maxKey} not found: languageCode={languageCode}, publicationCode={publicationCode}");
        
        return new KeyValuePair<int, BibleBook>(maxKey, maxBook);
    }

    public async Task<KeyValuePair<int, BibleBook>> GetNextBibleBook(string languageCode, string publicationCode,
        int bookNumber)
    {
        var books = await mediaService.GetBibleBooks(languageCode, publicationCode);
        if (books.Count == 0)
            throw new InvalidOperationException($"No bible books found: languageCode={languageCode}, publicationCode={publicationCode}");
        
        var previousBook = books.SkipWhile(kvp => kvp.Key <= bookNumber).FirstOrDefault();

        if (!previousBook.Equals(default(KeyValuePair<int, BibleBook>))) return previousBook;

        var minKey = books.Keys.Min();
        if (!books.TryGetValue(minKey, out var minBook))
            throw new InvalidOperationException($"Bible book with key {minKey} not found: languageCode={languageCode}, publicationCode={publicationCode}");
        
        return new KeyValuePair<int, BibleBook>(minKey, minBook);
    }

    private async Task<PlayItem> NextMusicUrlToPlay(AlarmSchedule schedule, bool next = false)
    {
        if (schedule.Music == null)
            throw new InvalidOperationException($"Music is null for schedule {schedule.Id}");
        
        switch (schedule.Music.MusicType)
        {
            case MusicType.Melodies:
                var melodyMusic = schedule.Music;
                var melodyTracks = await mediaService.GetMelodyMusicTracks(melodyMusic.PublicationCode);
                if (melodyTracks.Count == 0)
                    throw new InvalidOperationException($"No melody tracks found for publication {melodyMusic.PublicationCode}");

                var melodyTrackIndex = next ? melodyMusic.TrackNumber % melodyTracks.Count + 1 : melodyMusic.TrackNumber;
                if (melodyTrackIndex < 1 || melodyTrackIndex > melodyTracks.Count)
                    throw new InvalidOperationException($"Invalid track index {melodyTrackIndex} for {melodyTracks.Count} tracks");
                
                var melodyTrack = melodyTracks[melodyTrackIndex];
                if (melodyTrack.Source == null)
                    throw new InvalidOperationException($"Melody track {melodyTrackIndex} Source is null");
                
                return new PlayItem(new TrackMetadata
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
                if (vocalTracks.Count == 0)
                    throw new InvalidOperationException($"No vocal tracks found for language {vocalMusic.LanguageCode}, publication {vocalMusic.PublicationCode}");
                
                var vocalTrackIndex = next ? vocalMusic.TrackNumber % vocalTracks.Count + 1 : vocalMusic.TrackNumber;
                if (vocalTrackIndex < 1 || vocalTrackIndex > vocalTracks.Count)
                    throw new InvalidOperationException($"Invalid track index {vocalTrackIndex} for {vocalTracks.Count} tracks");
                
                var vocalTrack = vocalTracks[vocalTrackIndex];
                if (vocalTrack.Source == null)
                    throw new InvalidOperationException($"Vocal track {vocalTrackIndex} Source is null");
                
                return new PlayItem(new TrackMetadata
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

    public async Task<bool> ShouldResumeFromLastPositionAsync(int scheduleId)
    {
        using var scope = _scopeFactory.CreateScope();
        var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
        
        var schedule = await scheduleDbContext.AlarmSchedules
            .FirstOrDefaultAsync(x => x.Id == scheduleId, _cancellationTokenSource.Token);

        if (schedule == null)
            return false;

        // Resume is enabled when AlwaysPlayFromStart is false
        return !schedule.AlwaysPlayFromStart;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        
        _isDisposed = true;
        
        // Cancel and dispose cancellation token source
        try
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
        catch (Exception ex)
        {
            // Ignore errors during cancellation/disposal
            _logger.Warning(ex, "Error during cancellation token source disposal");
        }
        
        // Note: DbContext instances are now created via IServiceScopeFactory and disposed by the scope
        // mediaService (MediaService), IServiceScopeFactory, and IDispatcher are singletons
        // and should not be disposed here as they are managed by the DI container
    }
}