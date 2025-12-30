#nullable enable

using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Actions.Schedule;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Media;

public sealed class PlaylistService(
    ILogger logger,
    IMediaService mediaService,
    IDispatcher dispatcher,
    IAlarmScheduleService alarmScheduleService,
    IGeneralSettingsService generalSettingsService,
    IBibleTranslationService bibleTranslationService,
    IMelodyMusicService melodyMusicService,
    IDiskCacheService? diskCacheService) : IPlaylistService, IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool isDisposed;

    public async Task<int> GetRelevantScheduleToPlay()
    {
        // Query directly by Key - unique index ensures efficient lookup
        var lastSchedule = await generalSettingsService.GetGeneralSettingAsync(
            AppConstants.GeneralSettingsKeys.LastPlayedScheduleId, cancellationTokenSource.Token);

        AlarmSchedule? schedule = null;

        if (!string.IsNullOrEmpty(lastSchedule?.Value))
        {
            schedule = await alarmScheduleService.GetScheduleByIdAsync(
                int.Parse(lastSchedule.Value), false, false, cancellationTokenSource.Token);
        }

        if (schedule == null)
        {
            schedule = await alarmScheduleService.GetFirstScheduleOrDefaultAsync(
                false, false, cancellationTokenSource.Token);
        }

        if (schedule == null)
        {
            schedule = await AlarmSchedule.GetSampleSchedule(false, bibleTranslationService, melodyMusicService);
            schedule = await alarmScheduleService.AddScheduleAsync(schedule, cancellationTokenSource.Token);
        }

        return schedule.Id;
    }

    public async Task SaveLastPlayed(int scheduleId)
    {
        await generalSettingsService.SetGeneralSettingAsync(
            AppConstants.GeneralSettingsKeys.LastPlayedScheduleId,
            scheduleId.ToString(),
            cancellationTokenSource.Token);
    }

    public async Task MarkTrackAsPlayed(TrackMetadata trackMetadata)
    {
        var trackChanged = await CheckIfTrackChanged(trackMetadata);
        var nextTrackNumber = await GetNextTrackNumberIfNeeded(trackMetadata);

        var updatedSchedule = await UpdateScheduleForPlayedTrack(
            trackMetadata,
            nextTrackNumber);

        if (trackChanged)
        {
            dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
        }
    }

    private async Task<bool> CheckIfTrackChanged(TrackMetadata trackMetadata)
    {
        if (trackMetadata.PlayType != PlayType.Bible)
        {
            return false;
        }

        var scheduleBeforeUpdate = await alarmScheduleService.GetScheduleByIdAsync(
            (int)trackMetadata.ScheduleId, false, true, cancellationTokenSource.Token);

        if (scheduleBeforeUpdate?.BibleReadingSchedule == null)
        {
            return false;
        }

        var bibleReadingSchedule = scheduleBeforeUpdate.BibleReadingSchedule;
        return bibleReadingSchedule.BookNumber != trackMetadata.BookNumber ||
               bibleReadingSchedule.ChapterNumber != trackMetadata.ChapterNumber;
    }

    private async Task<int?> GetNextTrackNumberIfNeeded(TrackMetadata trackMetadata)
    {
        if (trackMetadata.PlayType != PlayType.Music)
        {
            return null;
        }

        var schedule = await alarmScheduleService.GetScheduleByIdAsync(
            (int)trackMetadata.ScheduleId, true, false, cancellationTokenSource.Token);

        if (schedule?.Music == null || schedule.Music.Repeat)
        {
            return null;
        }

        var next = await NextMusicUrlToPlay(schedule, true);
        return next.Metadata.TrackNumber;
    }

    private async Task<AlarmSchedule> UpdateScheduleForPlayedTrack(
        TrackMetadata trackMetadata,
        int? nextTrackNumber)
    {
        // Clear cache BEFORE save to prevent stale cache if process crashes
        const string CacheKey = "ScheduleList";
        diskCacheService?.Remove(CacheKey);
        
        return await alarmScheduleService.UpdateScheduleByIdAsync(
            (int)trackMetadata.ScheduleId,
            schedule => UpdateScheduleForPlayedTrackInternal(schedule, trackMetadata, nextTrackNumber),
            cancellationTokenSource.Token);
    }

    private static void UpdateScheduleForPlayedTrackInternal(
        AlarmSchedule schedule,
        TrackMetadata trackMetadata,
        int? nextTrackNumber)
    {
        if (trackMetadata.PlayType == PlayType.Music)
        {
            UpdateMusicTrack(schedule, nextTrackNumber);
        }
        else
        {
            UpdateBibleReadingTrack(schedule, trackMetadata);
        }
    }

    private static void UpdateMusicTrack(AlarmSchedule schedule, int? nextTrackNumber)
    {
        if (schedule.Music != null && !schedule.Music.Repeat && nextTrackNumber.HasValue)
        {
            schedule.Music.TrackNumber = nextTrackNumber.Value;
        }
    }

    private static void UpdateBibleReadingTrack(AlarmSchedule schedule, TrackMetadata trackMetadata)
    {
        var bibleReadingSchedule = schedule.BibleReadingSchedule ??
            throw new InvalidOperationException($"BibleReadingSchedule is null for schedule {schedule.Id}");

        bibleReadingSchedule.BookNumber = trackMetadata.BookNumber;
        bibleReadingSchedule.ChapterNumber = trackMetadata.ChapterNumber;
        bibleReadingSchedule.LanguageCode = trackMetadata.LanguageCode;
        bibleReadingSchedule.PublicationCode = trackMetadata.PublicationCode;
        bibleReadingSchedule.FinishedDuration = trackMetadata.FinishedDuration;
    }

    public async Task MarkTrackAsFinished(TrackMetadata trackMetadata)
    {
        // Get next track/chapter before updating
        var nextTrackInfo = await GetNextTrackInfoAsync(trackMetadata);

        // Clear cache BEFORE save to prevent stale cache if process crashes
        const string CacheKey = "ScheduleList";
        diskCacheService?.Remove(CacheKey);

        // Update schedule using service
        var updatedSchedule = await alarmScheduleService.UpdateScheduleByIdAsync(
            (int)trackMetadata.ScheduleId,
            schedule => UpdateScheduleForFinishedTrack(schedule, trackMetadata, nextTrackInfo),
            cancellationTokenSource.Token);

        // Update the Fluxor store to trigger state change and UI refresh
        // ScheduleListItem now subscribes to ApplicationState changes instead of TrackChangedMessage
        dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
    }

    private async Task<NextTrackInfo> GetNextTrackInfoAsync(TrackMetadata trackMetadata)
    {
        if (trackMetadata.PlayType == PlayType.Music)
        {
            var nextTrackNumber = await GetNextMusicTrackNumberAsync(trackMetadata);
            return new NextTrackInfo(nextTrackNumber, null);
        }
        else
        {
            var nextChapter = await GetNextBibleChapter(
                trackMetadata.LanguageCode,
                trackMetadata.PublicationCode,
                trackMetadata.BookNumber,
                trackMetadata.ChapterNumber);
            return new NextTrackInfo(null, nextChapter);
        }
    }

    private async Task<int?> GetNextMusicTrackNumberAsync(TrackMetadata trackMetadata)
    {
        var schedule = await alarmScheduleService.GetScheduleByIdAsync(
            (int)trackMetadata.ScheduleId, true, false, cancellationTokenSource.Token);
        if (schedule?.Music != null && !schedule.Music.Repeat)
        {
            var next = await NextMusicUrlToPlay(schedule, true);
            return next.Metadata.TrackNumber;
        }
        return null;
    }

    private void UpdateScheduleForFinishedTrack(
        AlarmSchedule schedule,
        TrackMetadata trackMetadata,
        NextTrackInfo nextTrackInfo)
    {
        if (trackMetadata.PlayType == PlayType.Music)
        {
            UpdateMusicTrackForFinished(schedule, nextTrackInfo.NextTrackNumber);
        }
        else
        {
            UpdateBibleReadingTrackForFinished(schedule, trackMetadata, nextTrackInfo.NextChapter);
        }
    }

    private static void UpdateMusicTrackForFinished(AlarmSchedule schedule, int? nextTrackNumber)
    {
        if (schedule.Music != null && !schedule.Music.Repeat && nextTrackNumber.HasValue)
        {
            schedule.Music.TrackNumber = nextTrackNumber.Value;
        }
    }

    private static void UpdateBibleReadingTrackForFinished(
        AlarmSchedule schedule,
        TrackMetadata trackMetadata,
        KeyValuePair<BibleBook, BibleChapter>? nextChapter)
    {
        var bibleReadingSchedule = schedule.BibleReadingSchedule ??
            throw new InvalidOperationException($"BibleReadingSchedule is null for schedule {schedule.Id}");

        if (nextChapter == null || nextChapter.Value.Key == null || nextChapter.Value.Value == null)
        {
            throw new InvalidOperationException("Next chapter Key or Value is null");
        }

        // Update book, chapter, AND translation to match the track that just finished
        bibleReadingSchedule.BookNumber = nextChapter.Value.Key.Number;
        bibleReadingSchedule.ChapterNumber = nextChapter.Value.Value.Number;
        bibleReadingSchedule.LanguageCode = trackMetadata.LanguageCode;
        bibleReadingSchedule.PublicationCode = trackMetadata.PublicationCode;
        bibleReadingSchedule.FinishedDuration = TimeSpan.Zero;
    }

    private record NextTrackInfo(int? NextTrackNumber, KeyValuePair<BibleBook, BibleChapter>? NextChapter);

    public async Task<PlayItem> NextTrack(int scheduleId)
    {
        var schedule = await alarmScheduleService.GetScheduleByIdAsync(
            scheduleId, true, true, cancellationTokenSource.Token) ?? throw new ArgumentException($"Invalid schedule Id {scheduleId}");
        if (schedule.MusicEnabled)
        {
            return await NextMusicUrlToPlay(schedule);
        }

        var bibleReadingSchedule = schedule.BibleReadingSchedule ?? throw new InvalidOperationException($"BibleReadingSchedule is null for schedule {scheduleId}");
        var bookNumber = bibleReadingSchedule.BookNumber;
        var chapter = bibleReadingSchedule.ChapterNumber;

        var chapterDetail = await mediaService.GetBibleChapter(bibleReadingSchedule.LanguageCode,
            bibleReadingSchedule.PublicationCode, bookNumber, chapter);

        if (chapterDetail == null)
        {
            logger.Error(
                $"Chapter: ${chapter}, book: {bookNumber}, language: {bibleReadingSchedule.LanguageCode}, pub code: {bibleReadingSchedule.PublicationCode} not in lookup. ");
            throw new InvalidOperationException($"Chapter not found: {chapter}, book: {bookNumber}");
        }

        var publicationCode = bibleReadingSchedule.PublicationCode;
        var languageCode = bibleReadingSchedule.LanguageCode;
        var url = chapterDetail.Source?.Url ?? string.Empty;
        var lookUpPath = chapterDetail.Source?.LookUpPath ?? string.Empty;

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
        ValidateScheduleId(scheduleId);

        var schedule = await LoadScheduleForTracks(scheduleId);
        var result = new List<PlayItem>();

        if (schedule.MusicEnabled)
        {
            result.Add(await NextMusicUrlToPlay(schedule));
        }

        var bibleTracks = await BuildBibleTracks(scheduleId, schedule);
        result.AddRange(bibleTracks);

        return result;
    }

    private static void ValidateScheduleId(int scheduleId)
    {
        if (scheduleId <= 0)
        {
            throw new ArgumentException($"Invalid schedule Id {scheduleId}. Schedule ID must be greater than 0.");
        }
    }

    private async Task<AlarmSchedule> LoadScheduleForTracks(int scheduleId)
    {
        return await alarmScheduleService.GetScheduleByIdAsync(
            scheduleId, true, true, cancellationTokenSource.Token) ??
            throw new ArgumentException($"Invalid schedule Id {scheduleId}");
    }

    private async Task<List<PlayItem>> BuildBibleTracks(int scheduleId, AlarmSchedule schedule)
    {
        var bibleReadingSchedule = schedule.BibleReadingSchedule ??
            throw new InvalidOperationException($"BibleReadingSchedule is null for schedule {scheduleId}");

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
                var next = await GetNextChapterInfo(bibleReadingSchedule, currentBookNumber, currentChapter.Number);
                currentBookNumber = next.BookNumber;
                currentChapter = next.Chapter;
                currentUrl = next.Url;
                currentLookUpPath = next.LookUpPath;
            }
        }

        return result;
    }

    private record ChapterInfo(int BookNumber, BibleChapter Chapter, string Url, string LookUpPath);

    private async Task<ChapterInfo> GetInitialChapterInfo(BibleReadingSchedule bibleReadingSchedule)
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

        if (ShouldSetFinishedDuration(markedSeekTrack, schedule, bibleReadingSchedule, trackMetadata))
        {
            trackMetadata.FinishedDuration = bibleReadingSchedule.FinishedDuration;
            markedSeekTrack = true;
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

    private async Task<ChapterInfo> GetNextChapterInfo(
        BibleReadingSchedule bibleReadingSchedule,
        int currentBookNumber,
        int currentChapterNumber)
    {
        var next = await GetNextBibleChapter(
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

    public async Task MoveToNextBibleChapter(int scheduleId)
    {
        var schedule = await GetScheduleWithBibleReadingAsync(scheduleId);
        if (schedule.BibleReadingSchedule == null)
        {
            return;
        }
        var next = await GetNextChapterForScheduleAsync(schedule.BibleReadingSchedule);

        var updatedSchedule = await UpdateScheduleToNextChapterAsync(scheduleId, next);
        dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
    }

    private async Task<AlarmSchedule> GetScheduleWithBibleReadingAsync(int scheduleId)
    {
        var schedule = await alarmScheduleService.GetScheduleByIdAsync(
            scheduleId, false, true, cancellationTokenSource.Token);

        if (schedule?.BibleReadingSchedule == null)
        {
            throw new ArgumentException($"BibleReadingSchedule is null for schedule {scheduleId}");
        }

        return schedule;
    }

    private async Task<KeyValuePair<BibleBook, BibleChapter>> GetNextChapterForScheduleAsync(BibleReadingSchedule bibleReadingSchedule)
    {
        return await GetNextBibleChapter(
            bibleReadingSchedule.LanguageCode,
            bibleReadingSchedule.PublicationCode,
            bibleReadingSchedule.BookNumber,
            bibleReadingSchedule.ChapterNumber);
    }

    private async Task<AlarmSchedule> UpdateScheduleToNextChapterAsync(int scheduleId, KeyValuePair<BibleBook, BibleChapter> next)
    {
        return await alarmScheduleService.UpdateScheduleByIdAsync(
            scheduleId,
            s =>
            {
                var brs = s.BibleReadingSchedule ?? throw new ArgumentException($"BibleReadingSchedule is null for schedule {scheduleId}");
                brs.BookNumber = next.Key.Number;
                brs.ChapterNumber = next.Value.Number;
                brs.FinishedDuration = TimeSpan.Zero;
            },
            cancellationTokenSource.Token);
    }

    public async Task MoveToPreviousBibleChapter(int scheduleId)
    {
        var schedule = await GetScheduleWithBibleReadingAsync(scheduleId);
        if (schedule.BibleReadingSchedule == null)
        {
            return;
        }
        var previous = await GetPreviousChapterForScheduleAsync(schedule.BibleReadingSchedule);
        ValidateChapterPair(previous, "Previous");

        var updatedSchedule = await UpdateScheduleToPreviousChapterAsync(scheduleId, previous);
        dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
    }

    private async Task<KeyValuePair<BibleBook, BibleChapter>> GetPreviousChapterForScheduleAsync(BibleReadingSchedule bibleReadingSchedule)
    {
        return await GetPreviousBibleChapter(
            bibleReadingSchedule.LanguageCode,
            bibleReadingSchedule.PublicationCode,
            bibleReadingSchedule.BookNumber,
            bibleReadingSchedule.ChapterNumber);
    }

    private static void ValidateChapterPair(KeyValuePair<BibleBook, BibleChapter> chapterPair, string context)
    {
        if (chapterPair.Key == null || chapterPair.Value == null)
        {
            throw new InvalidOperationException($"{context} chapter Key or Value is null");
        }
    }

    private async Task<AlarmSchedule> UpdateScheduleToPreviousChapterAsync(int scheduleId, KeyValuePair<BibleBook, BibleChapter> previous)
    {
        return await alarmScheduleService.UpdateScheduleByIdAsync(
            scheduleId,
            s =>
            {
                var brs = s.BibleReadingSchedule ?? throw new ArgumentException($"BibleReadingSchedule is null for schedule {scheduleId}");
                brs.BookNumber = previous.Key.Number;
                brs.ChapterNumber = previous.Value.Number;
                brs.FinishedDuration = TimeSpan.Zero;
            },
            cancellationTokenSource.Token);
    }

    public async Task<KeyValuePair<BibleBook, BibleChapter>> GetNextBibleChapter(string languageCode,
        string publicationCode, int bookNumber, int chapter)
    {
        var currentBook = await mediaService.GetBibleBook(languageCode, publicationCode, bookNumber) ?? throw new InvalidOperationException($"Bible book not found: languageCode={languageCode}, publicationCode={publicationCode}, bookNumber={bookNumber}");
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
        var currentBook = await mediaService.GetBibleBook(languageCode, publicationCode, bookNumber) ?? throw new InvalidOperationException($"Bible book not found: languageCode={languageCode}, publicationCode={publicationCode}, bookNumber={bookNumber}");
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

        return schedule.Music.MusicType switch
        {
            MusicType.Melodies => await GetNextMelodyTrackAsync(schedule, next),
            MusicType.Vocals => await GetNextVocalTrackAsync(schedule, next),
            _ => throw new ApplicationException("Invalid MusicType.")
        };
    }

    private async Task<PlayItem> GetNextMelodyTrackAsync(AlarmSchedule schedule, bool next)
    {
        var melodyMusic = schedule.Music;
        if (melodyMusic == null)
        {
            throw new InvalidOperationException("Schedule music is null");
        }
        var melodyTracks = await mediaService.GetMelodyMusicTracks(melodyMusic.PublicationCode);
        if (melodyTracks.Count == 0)
        {
            throw new InvalidOperationException($"No melody tracks found for publication {melodyMusic.PublicationCode}");
        }

        var melodyTrackIndex = CalculateTrackIndex(melodyMusic.TrackNumber, melodyTracks.Count, next);
        var melodyTrack = melodyTracks[melodyTrackIndex];
        ValidateTrackSource(melodyTrack.Source, melodyTrackIndex, "Melody");

        return CreateMelodyPlayItem(schedule, melodyMusic, melodyTrack);
    }

    private async Task<PlayItem> GetNextVocalTrackAsync(AlarmSchedule schedule, bool next)
    {
        var vocalMusic = schedule.Music;
        if (vocalMusic == null)
        {
            throw new InvalidOperationException("Schedule music is null");
        }
        var vocalTracks = await mediaService.GetVocalMusicTracks(vocalMusic.LanguageCode, vocalMusic.PublicationCode);
        if (vocalTracks.Count == 0)
        {
            throw new InvalidOperationException($"No vocal tracks found for language {vocalMusic.LanguageCode}, publication {vocalMusic.PublicationCode}");
        }

        var vocalTrackIndex = CalculateTrackIndex(vocalMusic.TrackNumber, vocalTracks.Count, next);
        var vocalTrack = vocalTracks[vocalTrackIndex];
        ValidateTrackSource(vocalTrack.Source, vocalTrackIndex, "Vocal");

        return CreateVocalPlayItem(schedule, vocalMusic, vocalTrack);
    }

    private static int CalculateTrackIndex(int currentTrackNumber, int totalTracks, bool next)
    {
        var trackIndex = next ? currentTrackNumber % totalTracks + 1 : currentTrackNumber;
        if (trackIndex < 1 || trackIndex > totalTracks)
        {
            throw new InvalidOperationException($"Invalid track index {trackIndex} for {totalTracks} tracks");
        }
        return trackIndex;
    }

    private static void ValidateTrackSource(AudioSource? source, int trackIndex, string trackType)
    {
        if (source == null)
        {
            throw new InvalidOperationException($"{trackType} track {trackIndex} Source is null");
        }
    }

    private static PlayItem CreateMelodyPlayItem(AlarmSchedule schedule, AlarmMusic melodyMusic, MusicTrack melodyTrack)
    {
        if (melodyTrack.Source == null)
        {
            throw new InvalidOperationException($"Melody track {melodyTrack.Number} Source is null");
        }
        return new PlayItem(new TrackMetadata
        {
            ScheduleId = schedule.Id,
            PublicationCode = melodyMusic.PublicationCode,
            TrackNumber = melodyTrack.Number,
            LookUpPath = melodyTrack.Source.LookUpPath
        }, melodyTrack.Source.Url);
    }

    private static PlayItem CreateVocalPlayItem(AlarmSchedule schedule, AlarmMusic vocalMusic, MusicTrack vocalTrack)
    {
        if (vocalTrack.Source == null)
        {
            throw new InvalidOperationException($"Vocal track {vocalTrack.Number} Source is null");
        }
        return new PlayItem(new TrackMetadata
        {
            ScheduleId = schedule.Id,
            PublicationCode = vocalMusic.PublicationCode,
            LanguageCode = vocalMusic.LanguageCode,
            TrackNumber = vocalTrack.Number,
            LookUpPath = vocalTrack.Source.LookUpPath
        }, vocalTrack.Source.Url);
    }

    public async Task<bool> ShouldResumeFromLastPositionAsync(int scheduleId)
    {
        var schedule = await alarmScheduleService.GetScheduleByIdAsync(
            scheduleId, false, false, cancellationTokenSource.Token);

        if (schedule == null)
        {
            return false;
        }

        // Resume is enabled when AlwaysPlayFromStart is false
        return !schedule.AlwaysPlayFromStart;
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // Cancel and dispose cancellation token source
        try
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }
        catch (Exception ex)
        {
            // Ignore errors during cancellation/disposal
            logger.Warning(ex, "Error during cancellation token source disposal");
        }

        // Note: DbContext instances are now created via IServiceScopeFactory and disposed by the scope
        // mediaService (MediaService), IServiceScopeFactory, and IDispatcher are singletons
        // and should not be disposed here as they are managed by the DI container
    }
}
