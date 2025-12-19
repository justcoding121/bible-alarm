#nullable enable

using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Actions.Schedule;
using Microsoft.EntityFrameworkCore;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Media;

public class PlaylistService(
    ILogger logger,
    IMediaService mediaService,
    IDispatcher dispatcher,
    IAlarmScheduleService alarmScheduleService,
    IGeneralSettingsService generalSettingsService,
    IBibleTranslationService bibleTranslationService,
    IMelodyMusicService melodyMusicService)
    : IPlaylistService, IDisposable
{
    private readonly ILogger _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly IAlarmScheduleService _alarmScheduleService = alarmScheduleService;
    private readonly IGeneralSettingsService _generalSettingsService = generalSettingsService;
    private readonly IBibleTranslationService _bibleTranslationService = bibleTranslationService;
    private readonly IMelodyMusicService _melodyMusicService = melodyMusicService;
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private bool _isDisposed;

    public async Task<int> GetRelevantScheduleToPlay()
    {
        // Query directly by Key - unique index ensures efficient lookup
        var lastSchedule = await _generalSettingsService.GetGeneralSettingAsync(
            AppConstants.GeneralSettingsKeys.LastPlayedScheduleId, _cancellationTokenSource.Token);

        AlarmSchedule? schedule = null;

        if (!string.IsNullOrEmpty(lastSchedule?.Value))
        {
            schedule = await _alarmScheduleService.GetScheduleByIdAsync(
                int.Parse(lastSchedule.Value), false, false, _cancellationTokenSource.Token);
        }

        if (schedule == null)
        {
            schedule = await _alarmScheduleService.GetFirstScheduleOrDefaultAsync(
                false, false, _cancellationTokenSource.Token);
        }

        if (schedule == null)
        {
            schedule = await AlarmSchedule.GetSampleSchedule(false, _bibleTranslationService, _melodyMusicService);
            schedule = await _alarmScheduleService.AddScheduleAsync(schedule, _cancellationTokenSource.Token);
        }

        return schedule.Id;
    }

    public async Task SaveLastPlayed(int scheduleId)
    {
        await _generalSettingsService.SetGeneralSettingAsync(
            AppConstants.GeneralSettingsKeys.LastPlayedScheduleId,
            scheduleId.ToString(),
            _cancellationTokenSource.Token);
    }

    public async Task MarkTrackAsPlayed(TrackMetadata trackMetadata)
    {
        var trackChanged = false;
        AlarmSchedule? scheduleBeforeUpdate = null;

        // Get schedule before update to check if chapter changed
        if (trackMetadata.PlayType == PlayType.Bible)
        {
            scheduleBeforeUpdate = await _alarmScheduleService.GetScheduleByIdAsync(
                (int)trackMetadata.ScheduleId, false, true, _cancellationTokenSource.Token);

            if (scheduleBeforeUpdate?.BibleReadingSchedule != null)
            {
                var bibleReadingSchedule = scheduleBeforeUpdate.BibleReadingSchedule;
                // Check if chapter changed (not just progress)
                if (bibleReadingSchedule.BookNumber != trackMetadata.BookNumber
                    || bibleReadingSchedule.ChapterNumber != trackMetadata.ChapterNumber)
                {
                    trackChanged = true;
                }
            }
        }

        // For music, get next track number before updating
        int? nextTrackNumber = null;
        if (trackMetadata.PlayType == PlayType.Music)
        {
            var schedule = await _alarmScheduleService.GetScheduleByIdAsync(
                (int)trackMetadata.ScheduleId, true, false, _cancellationTokenSource.Token);
            if (schedule?.Music != null && !schedule.Music.Repeat)
            {
                var next = await NextMusicUrlToPlay(schedule, true);
                nextTrackNumber = next.Metadata.TrackNumber;
            }
        }

        // Update schedule using service
        var updatedSchedule = await _alarmScheduleService.UpdateScheduleByIdAsync(
            (int)trackMetadata.ScheduleId,
            schedule =>
            {
                if (trackMetadata.PlayType == PlayType.Music)
                {
                    if (schedule.Music != null && !schedule.Music.Repeat && nextTrackNumber.HasValue)
                    {
                        schedule.Music.TrackNumber = nextTrackNumber.Value;
                    }
                }
                else
                {
                    var bibleReadingSchedule = schedule.BibleReadingSchedule;
                    if (bibleReadingSchedule == null)
                    {
                        throw new InvalidOperationException($"BibleReadingSchedule is null for schedule {schedule.Id}");
                    }

                    // Update book, chapter, translation, AND language to match the current track
                    bibleReadingSchedule.BookNumber = trackMetadata.BookNumber;
                    bibleReadingSchedule.ChapterNumber = trackMetadata.ChapterNumber;
                    bibleReadingSchedule.LanguageCode = trackMetadata.LanguageCode;
                    bibleReadingSchedule.PublicationCode = trackMetadata.PublicationCode;
                    bibleReadingSchedule.FinishedDuration = trackMetadata.FinishedDuration;
                }
            },
            _cancellationTokenSource.Token);

        if (trackChanged)
        {
            // Only update state when chapter/track actually changed (not just progress)
            // Update the Fluxor store to trigger state change and UI refresh
            // ScheduleListItem now subscribes to ApplicationState changes instead of TrackChangedMessage
            _dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
        }
    }

    public async Task MarkTrackAsFinished(TrackMetadata trackMetadata)
    {
        // Get next track/chapter before updating
        int? nextTrackNumber = null;
        KeyValuePair<BibleBook, BibleChapter>? nextChapter = null;

        if (trackMetadata.PlayType == PlayType.Music)
        {
            var schedule = await _alarmScheduleService.GetScheduleByIdAsync(
                (int)trackMetadata.ScheduleId, true, false, _cancellationTokenSource.Token);
            if (schedule?.Music != null && !schedule.Music.Repeat)
            {
                var next = await NextMusicUrlToPlay(schedule, true);
                nextTrackNumber = next.Metadata.TrackNumber;
            }
        }
        else
        {
            nextChapter = await GetNextBibleChapter(trackMetadata.LanguageCode, trackMetadata.PublicationCode,
                trackMetadata.BookNumber, trackMetadata.ChapterNumber);
        }

        // Update schedule using service
        var updatedSchedule = await _alarmScheduleService.UpdateScheduleByIdAsync(
            (int)trackMetadata.ScheduleId,
            schedule =>
            {
                if (trackMetadata.PlayType == PlayType.Music)
                {
                    if (schedule.Music != null && !schedule.Music.Repeat && nextTrackNumber.HasValue)
                    {
                        schedule.Music.TrackNumber = nextTrackNumber.Value;
                    }
                }
                else
                {
                    var bibleReadingSchedule = schedule.BibleReadingSchedule;
                    if (bibleReadingSchedule == null)
                    {
                        throw new InvalidOperationException($"BibleReadingSchedule is null for schedule {schedule.Id}");
                    }

                    if (nextChapter == null || nextChapter.Value.Key == null || nextChapter.Value.Value == null)
                    {
                        throw new InvalidOperationException($"Next chapter Key or Value is null");
                    }

                    // Update book, chapter, AND translation to match the track that just finished
                    bibleReadingSchedule.BookNumber = nextChapter.Value.Key.Number;
                    bibleReadingSchedule.ChapterNumber = nextChapter.Value.Value.Number;
                    bibleReadingSchedule.LanguageCode = trackMetadata.LanguageCode;
                    bibleReadingSchedule.PublicationCode = trackMetadata.PublicationCode;
                    bibleReadingSchedule.FinishedDuration = TimeSpan.Zero;
                }
            },
            _cancellationTokenSource.Token);

        // Update the Fluxor store to trigger state change and UI refresh
        // ScheduleListItem now subscribes to ApplicationState changes instead of TrackChangedMessage
        _dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
    }

    public async Task<PlayItem> NextTrack(int scheduleId)
    {
        var schedule = await _alarmScheduleService.GetScheduleByIdAsync(
            scheduleId, true, true, _cancellationTokenSource.Token);

        if (schedule == null)
        {
            throw new ArgumentException($"Invalid schedule Id {scheduleId}");
        }

        if (schedule.MusicEnabled)
        {
            return await NextMusicUrlToPlay(schedule);
        }

        var bibleReadingSchedule = schedule.BibleReadingSchedule;

        var bookNumber = bibleReadingSchedule.BookNumber;
        var chapter = bibleReadingSchedule.ChapterNumber;

        var chapterDetail = await mediaService.GetBibleChapter(bibleReadingSchedule.LanguageCode,
            bibleReadingSchedule.PublicationCode, bookNumber, chapter);

        if (chapterDetail == null)
        {
            _logger.Error(
                $"Chapter: ${chapter}, book: {bookNumber}, language: {bibleReadingSchedule.LanguageCode}, pub code: {bibleReadingSchedule.PublicationCode} not in lookup. ");
        }

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
        var result = new List<PlayItem>();

        var schedule = await _alarmScheduleService.GetScheduleByIdAsync(
            scheduleId, true, true, _cancellationTokenSource.Token);

        if (schedule == null)
        {
            throw new ArgumentException($"Invalid schedule Id {scheduleId}");
        }

        var numberOfChaptersToRead = schedule.NumberOfChaptersToRead;

        if (schedule.MusicEnabled)
        {
            result.Add(await NextMusicUrlToPlay(schedule));
        }

        var bibleReadingSchedule = schedule.BibleReadingSchedule;
        if (bibleReadingSchedule == null)
        {
            throw new InvalidOperationException($"BibleReadingSchedule is null for schedule {scheduleId}");
        }

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
        {
            throw new InvalidOperationException($"Chapter {chapter} Source is null in book {bookNumber}");
        }

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
            {
                trackMetadata.FinishedDuration = bibleReadingSchedule.FinishedDuration;
            }

            markedSeekTrack = true;

            result.Add(new PlayItem(trackMetadata, url));

            numberOfChaptersToRead--;

            var next = await GetNextBibleChapter(bibleReadingSchedule.LanguageCode,
                bibleReadingSchedule.PublicationCode,
                bookNumber, chapter);

            if (next.Key == null || next.Value == null)
            {
                throw new InvalidOperationException($"Next chapter Key or Value is null");
            }

            if (next.Value.Source == null)
            {
                throw new InvalidOperationException($"Next chapter Source is null");
            }

            bookNumber = next.Key.Number;
            chapter = next.Value.Number;
            url = next.Value.Source.Url;
            lookUpPath = next.Value.Source.LookUpPath;
        }

        return result;
    }

    public async Task MoveToNextBibleChapter(int scheduleId)
    {
        // Get current schedule and next chapter before updating
        var schedule = await _alarmScheduleService.GetScheduleByIdAsync(
            scheduleId, false, true, _cancellationTokenSource.Token);

        if (schedule?.BibleReadingSchedule == null)
        {
            throw new ArgumentException($"BibleReadingSchedule is null for schedule {scheduleId}");
        }

        var bibleReadingSchedule = schedule.BibleReadingSchedule;
        var next = await GetNextBibleChapter(
            bibleReadingSchedule.LanguageCode,
            bibleReadingSchedule.PublicationCode,
            bibleReadingSchedule.BookNumber,
            bibleReadingSchedule.ChapterNumber);

        var updatedSchedule = await _alarmScheduleService.UpdateScheduleByIdAsync(
            scheduleId,
            s =>
            {
                var brs = s.BibleReadingSchedule;
                if (brs == null)
                {
                    throw new ArgumentException($"BibleReadingSchedule is null for schedule {scheduleId}");
                }

                brs.BookNumber = next.Key.Number;
                brs.ChapterNumber = next.Value.Number;
                brs.FinishedDuration = TimeSpan.Zero;
            },
            _cancellationTokenSource.Token);

        // Update the Fluxor store to trigger state change and UI refresh
        _dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
    }

    public async Task MoveToPreviousBibleChapter(int scheduleId)
    {
        // Get current schedule and previous chapter before updating
        var schedule = await _alarmScheduleService.GetScheduleByIdAsync(
            scheduleId, false, true, _cancellationTokenSource.Token);

        if (schedule?.BibleReadingSchedule == null)
        {
            throw new ArgumentException($"BibleReadingSchedule is null for schedule {scheduleId}");
        }

        var bibleReadingSchedule = schedule.BibleReadingSchedule;
        var previous = await GetPreviousBibleChapter(
            bibleReadingSchedule.LanguageCode,
            bibleReadingSchedule.PublicationCode,
            bibleReadingSchedule.BookNumber,
            bibleReadingSchedule.ChapterNumber);

        if (previous.Key == null || previous.Value == null)
        {
            throw new InvalidOperationException($"Previous chapter Key or Value is null");
        }

        var updatedSchedule = await _alarmScheduleService.UpdateScheduleByIdAsync(
            scheduleId,
            s =>
            {
                var brs = s.BibleReadingSchedule;
                if (brs == null)
                {
                    throw new ArgumentException($"BibleReadingSchedule is null for schedule {scheduleId}");
                }

                brs.BookNumber = previous.Key.Number;
                brs.ChapterNumber = previous.Value.Number;
                brs.FinishedDuration = TimeSpan.Zero;
            },
            _cancellationTokenSource.Token);

        // Update the Fluxor store to trigger state change and UI refresh
        _dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
    }

    public async Task<KeyValuePair<BibleBook, BibleChapter>> GetNextBibleChapter(string languageCode,
        string publicationCode, int bookNumber, int chapter)
    {
        var currentBook = await mediaService.GetBibleBook(languageCode, publicationCode, bookNumber);
        if (currentBook == null)
        {
            throw new InvalidOperationException($"Bible book not found: languageCode={languageCode}, publicationCode={publicationCode}, bookNumber={bookNumber}");
        }

        var chapters = await mediaService.GetBibleChapters(languageCode, publicationCode, bookNumber);
        var nextChapter = chapters.SkipWhile(kvp => kvp.Key <= chapter).FirstOrDefault();

        if (!nextChapter.Equals(default(KeyValuePair<int, BibleChapter>)))
        {
            return new KeyValuePair<BibleBook, BibleChapter>(currentBook, nextChapter.Value);
        }

        var nextBook = await GetNextBibleBook(languageCode, publicationCode, bookNumber);
        if (nextBook.Value == null)
        {
            throw new InvalidOperationException($"Next bible book not found: languageCode={languageCode}, publicationCode={publicationCode}, bookNumber={bookNumber}");
        }

        chapters = await mediaService.GetBibleChapters(languageCode, publicationCode, nextBook.Key);
        if (chapters.Count == 0)
        {
            throw new InvalidOperationException($"No chapters in next book: languageCode={languageCode}, publicationCode={publicationCode}, bookNumber={nextBook.Key}");
        }

        // Start at the first chapter of the next book (index 0)
        return new KeyValuePair<BibleBook, BibleChapter>(nextBook.Value, chapters.ElementAt(0).Value);
    }

    public async Task<KeyValuePair<BibleBook, BibleChapter>> GetPreviousBibleChapter(string languageCode,
        string publicationCode, int bookNumber, int chapter)
    {
        var currentBook = await mediaService.GetBibleBook(languageCode, publicationCode, bookNumber);
        if (currentBook == null)
        {
            throw new InvalidOperationException($"Bible book not found: languageCode={languageCode}, publicationCode={publicationCode}, bookNumber={bookNumber}");
        }

        var chapters = await mediaService.GetBibleChapters(languageCode, publicationCode, bookNumber);
        var previousChapter = chapters.Reverse().SkipWhile(kvp => kvp.Key >= chapter).FirstOrDefault();

        if (!previousChapter.Equals(default(KeyValuePair<int, BibleChapter>)))
        {
            return new KeyValuePair<BibleBook, BibleChapter>(currentBook, previousChapter.Value);
        }

        var previousBook = await GetPreviousBibleBook(languageCode, publicationCode, bookNumber);
        if (previousBook.Value == null)
        {
            throw new InvalidOperationException($"Previous bible book not found: languageCode={languageCode}, publicationCode={publicationCode}, bookNumber={bookNumber}");
        }

        chapters = await mediaService.GetBibleChapters(languageCode, publicationCode, previousBook.Key);
        if (chapters.Count == 0)
        {
            throw new InvalidOperationException($"No chapters in previous book: languageCode={languageCode}, publicationCode={publicationCode}, bookNumber={previousBook.Key}");
        }

        return new KeyValuePair<BibleBook, BibleChapter>(previousBook.Value, chapters.ElementAt(chapters.Count - 1).Value);
    }

    public async Task<KeyValuePair<int, BibleBook>> GetPreviousBibleBook(string languageCode, string publicationCode,
        int bookNumber)
    {
        var books = await mediaService.GetBibleBooks(languageCode, publicationCode);
        if (books.Count == 0)
        {
            throw new InvalidOperationException($"No bible books found: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        var previousBook = books.Reverse().SkipWhile(kvp => kvp.Key >= bookNumber).FirstOrDefault();

        if (!previousBook.Equals(default(KeyValuePair<int, BibleBook>)))
        {
            return previousBook;
        }

        var maxKey = books.Keys.Max();
        if (!books.TryGetValue(maxKey, out var maxBook))
        {
            throw new InvalidOperationException($"Bible book with key {maxKey} not found: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        return new KeyValuePair<int, BibleBook>(maxKey, maxBook);
    }

    public async Task<KeyValuePair<int, BibleBook>> GetNextBibleBook(string languageCode, string publicationCode,
        int bookNumber)
    {
        var books = await mediaService.GetBibleBooks(languageCode, publicationCode);
        if (books.Count == 0)
        {
            throw new InvalidOperationException($"No bible books found: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        var previousBook = books.SkipWhile(kvp => kvp.Key <= bookNumber).FirstOrDefault();

        if (!previousBook.Equals(default(KeyValuePair<int, BibleBook>)))
        {
            return previousBook;
        }

        var minKey = books.Keys.Min();
        if (!books.TryGetValue(minKey, out var minBook))
        {
            throw new InvalidOperationException($"Bible book with key {minKey} not found: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        return new KeyValuePair<int, BibleBook>(minKey, minBook);
    }

    private async Task<PlayItem> NextMusicUrlToPlay(AlarmSchedule schedule, bool next = false)
    {
        if (schedule.Music == null)
        {
            throw new InvalidOperationException($"Music is null for schedule {schedule.Id}");
        }

        switch (schedule.Music.MusicType)
        {
            case MusicType.Melodies:
                var melodyMusic = schedule.Music;
                var melodyTracks = await mediaService.GetMelodyMusicTracks(melodyMusic.PublicationCode);
                if (melodyTracks.Count == 0)
                {
                    throw new InvalidOperationException($"No melody tracks found for publication {melodyMusic.PublicationCode}");
                }

                var melodyTrackIndex = next ? melodyMusic.TrackNumber % melodyTracks.Count + 1 : melodyMusic.TrackNumber;
                if (melodyTrackIndex < 1 || melodyTrackIndex > melodyTracks.Count)
                {
                    throw new InvalidOperationException($"Invalid track index {melodyTrackIndex} for {melodyTracks.Count} tracks");
                }

                var melodyTrack = melodyTracks[melodyTrackIndex];
                if (melodyTrack.Source == null)
                {
                    throw new InvalidOperationException($"Melody track {melodyTrackIndex} Source is null");
                }

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
                {
                    throw new InvalidOperationException($"No vocal tracks found for language {vocalMusic.LanguageCode}, publication {vocalMusic.PublicationCode}");
                }

                var vocalTrackIndex = next ? vocalMusic.TrackNumber % vocalTracks.Count + 1 : vocalMusic.TrackNumber;
                if (vocalTrackIndex < 1 || vocalTrackIndex > vocalTracks.Count)
                {
                    throw new InvalidOperationException($"Invalid track index {vocalTrackIndex} for {vocalTracks.Count} tracks");
                }

                var vocalTrack = vocalTracks[vocalTrackIndex];
                if (vocalTrack.Source == null)
                {
                    throw new InvalidOperationException($"Vocal track {vocalTrackIndex} Source is null");
                }

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
        var schedule = await _alarmScheduleService.GetScheduleByIdAsync(
            scheduleId, false, false, _cancellationTokenSource.Token);

        if (schedule == null)
        {
            return false;
        }

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