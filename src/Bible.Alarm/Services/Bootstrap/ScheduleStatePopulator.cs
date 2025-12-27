#nullable enable

using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Models;
using Serilog;

namespace Bible.Alarm.Services.Bootstrap;

/// <summary>
/// Helper class for populating schedule state items with display names and metadata.
/// </summary>
internal sealed class ScheduleStatePopulator
{
    private readonly IBibleTranslationService? bibleTranslationService;
    private readonly IBibleBookService? bibleBookService;
    private readonly IMapper mapper;
    private readonly IMediaService? mediaService;
    private readonly IMelodyMusicService? melodyMusicService;

    public ScheduleStatePopulator(
        IBibleTranslationService? bibleTranslationService,
        IBibleBookService? bibleBookService,
        IMapper mapper,
        IMediaService? mediaService,
        IMelodyMusicService? melodyMusicService)
    {
        this.bibleTranslationService = bibleTranslationService;
        this.bibleBookService = bibleBookService;
        this.mapper = mapper;
        this.mediaService = mediaService;
        this.melodyMusicService = melodyMusicService;
    }

    public async Task<ObservableHashSet<ScheduleStateItem>> PopulateAsync(
        List<AlarmSchedule> alarmSchedules,
        Dictionary<string, Language>? languagesDict)
    {
        // Optimize: Batch load all required data upfront to avoid N+1 queries
        var lookupData = await LoadAllLookupDataAsync(alarmSchedules);

        // Process schedules in parallel instead of sequentially
        var scheduleTasks = alarmSchedules.Select(async schedule =>
        {
            var scheduleStateItem = mapper.Map<ScheduleStateItem>(schedule);

            // Use pre-loaded lookup data instead of making individual queries
            PopulateBibleReadingDisplayNamesFromCache(
                schedule,
                scheduleStateItem,
                lookupData,
                languagesDict);

            PopulateMusicDisplayNamesFromCache(
                schedule,
                scheduleStateItem,
                lookupData);

            return scheduleStateItem;
        });

        var scheduleStateItems = await Task.WhenAll(scheduleTasks);

        // Batch populate default music for all schedules that need it
        await PopulateDefaultMusicBatchAsync(
            alarmSchedules,
            scheduleStateItems);

        var initialSchedules = new ObservableHashSet<ScheduleStateItem>();
        foreach (var item in scheduleStateItems)
        {
            initialSchedules.Add(item);
        }
        return initialSchedules;
    }

    private async Task<LookupData> LoadAllLookupDataAsync(List<AlarmSchedule> alarmSchedules)
    {
        // Collect all unique keys needed
        var translationKeys = new HashSet<(string LanguageCode, string PublicationCode)>();
        var bookKeys = new HashSet<(string LanguageCode, string PublicationCode, int BookNumber)>();
        var vocalMusicLanguageCodes = new HashSet<string>();
        var vocalMusicKeys = new HashSet<(string LanguageCode, string PublicationCode)>();
        var vocalTrackKeys = new HashSet<(string LanguageCode, string PublicationCode)>();
        var melodyPublicationCodes = new HashSet<string>();

        foreach (var schedule in alarmSchedules)
        {
            // Collect Bible reading keys
            if (schedule.BibleReadingSchedule != null)
            {
                var br = schedule.BibleReadingSchedule;
                if (!string.IsNullOrWhiteSpace(br.LanguageCode) && !string.IsNullOrWhiteSpace(br.PublicationCode))
                {
                    translationKeys.Add((br.LanguageCode, br.PublicationCode));
                    if (br.BookNumber > 0)
                    {
                        bookKeys.Add((br.LanguageCode, br.PublicationCode, br.BookNumber));
                    }
                }
            }

            // Collect music keys
            if (schedule.Music != null)
            {
                var music = schedule.Music;
                if (music.MusicType == Shared.Models.Enums.MusicType.Vocals)
                {
                    if (!string.IsNullOrWhiteSpace(music.LanguageCode))
                    {
                        vocalMusicLanguageCodes.Add(music.LanguageCode);
                        if (!string.IsNullOrWhiteSpace(music.PublicationCode))
                        {
                            vocalMusicKeys.Add((music.LanguageCode, music.PublicationCode));
                            if (music.TrackNumber > 0)
                            {
                                vocalTrackKeys.Add((music.LanguageCode, music.PublicationCode));
                            }
                        }
                    }
                }
                else if (music.MusicType == Shared.Models.Enums.MusicType.Melodies)
                {
                    if (!string.IsNullOrWhiteSpace(music.PublicationCode))
                    {
                        melodyPublicationCodes.Add(music.PublicationCode);
                    }
                }
            }
        }

        // Load all data in parallel
        var translationTasks = translationKeys.Select(async key =>
        {
            try
            {
                var translation = await bibleTranslationService?.GetByLanguageAndCodeWithBooksAsync(
                    key.LanguageCode, key.PublicationCode);
                return (Key: key, Translation: translation);
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "Error loading translation {LanguageCode}/{PublicationCode}",
                    key.LanguageCode, key.PublicationCode);
                return (Key: key, Translation: (BibleTranslation?)null);
            }
        }).ToList();

        var bookTasks = bookKeys.Select(async key =>
        {
            try
            {
                var bookName = await bibleBookService?.GetBookNameAsync(
                    key.LanguageCode, key.PublicationCode, key.BookNumber);
                return (Key: key, BookName: bookName);
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "Error loading book {LanguageCode}/{PublicationCode}/{BookNumber}",
                    key.LanguageCode, key.PublicationCode, key.BookNumber);
                return (Key: key, BookName: (string?)null);
            }
        }).ToList();

        var vocalLanguagesTask = mediaService != null && vocalMusicLanguageCodes.Any()
            ? mediaService.GetVocalMusicLanguages()
            : Task.FromResult<Dictionary<string, Language>>(new Dictionary<string, Language>());

        var vocalReleasesTasks = vocalMusicKeys.GroupBy(k => k.LanguageCode).Select(async group =>
        {
            try
            {
                var releases = await mediaService?.GetVocalMusicReleases(group.Key);
                return (LanguageCode: group.Key, Releases: releases ?? new Dictionary<string, VocalMusic>());
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "Error loading vocal music releases for {LanguageCode}", group.Key);
                return (LanguageCode: group.Key, Releases: new Dictionary<string, VocalMusic>());
            }
        }).ToList();

        var vocalTracksTasks = vocalTrackKeys.Select(async key =>
        {
            try
            {
                var tracks = await mediaService?.GetVocalMusicTracks(key.LanguageCode, key.PublicationCode);
                return (Key: key, Tracks: tracks ?? new SortedDictionary<int, MusicTrack>());
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "Error loading vocal tracks {LanguageCode}/{PublicationCode}",
                    key.LanguageCode, key.PublicationCode);
                return (Key: key, Tracks: new SortedDictionary<int, MusicTrack>());
            }
        }).ToList();

        var melodyTracksTasks = melodyPublicationCodes.Select(async pubCode =>
        {
            try
            {
                var tracks = await mediaService?.GetMelodyMusicTracks(pubCode);
                return (PublicationCode: pubCode, Tracks: tracks ?? new SortedDictionary<int, MusicTrack>());
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "Error loading melody tracks {PublicationCode}", pubCode);
                return (PublicationCode: pubCode, Tracks: new SortedDictionary<int, MusicTrack>());
            }
        }).ToList();

        // Wait for all batch loads to complete in parallel
        await Task.WhenAll(
            Task.WhenAll(translationTasks),
            Task.WhenAll(bookTasks),
            vocalLanguagesTask,
            Task.WhenAll(vocalReleasesTasks),
            Task.WhenAll(vocalTracksTasks),
            Task.WhenAll(melodyTracksTasks));

        // Build lookup dictionaries
        var translationsDict = translationTasks
            .Where(t => t.Result.Translation != null)
            .ToDictionary(t => t.Result.Key, t => t.Result.Translation!);

        var booksDict = bookTasks
            .Where(t => !string.IsNullOrWhiteSpace(t.Result.BookName))
            .ToDictionary(t => t.Result.Key, t => t.Result.BookName!);

        var vocalLanguagesDict = await vocalLanguagesTask;

        var vocalReleasesDict = (await Task.WhenAll(vocalReleasesTasks))
            .SelectMany(r => r.Releases.Select(kvp => new { Key = (r.LanguageCode, PublicationCode: kvp.Key), Release = kvp.Value }))
            .ToDictionary(x => x.Key, x => x.Release);

        var vocalTracksDict = (await Task.WhenAll(vocalTracksTasks))
            .ToDictionary(t => t.Key, t => t.Tracks);

        var melodyTracksDict = (await Task.WhenAll(melodyTracksTasks))
            .ToDictionary(t => t.PublicationCode, t => t.Tracks);

        return new LookupData(
            Translations: translationsDict,
            Books: booksDict,
            VocalLanguages: vocalLanguagesDict,
            VocalReleases: vocalReleasesDict,
            VocalTracks: vocalTracksDict,
            MelodyTracks: melodyTracksDict);
    }

    private void PopulateBibleReadingDisplayNamesFromCache(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        LookupData lookupData,
        Dictionary<string, Language>? languagesDict)
    {
        if (schedule.BibleReadingSchedule == null)
        {
            return;
        }

        var bibleReading = schedule.BibleReadingSchedule;
        SetBibleReadingLanguageName(schedule, scheduleStateItem, bibleReading, languagesDict);

        // Use cached translation
        if (!string.IsNullOrWhiteSpace(bibleReading.LanguageCode) &&
            !string.IsNullOrWhiteSpace(bibleReading.PublicationCode))
        {
            var translationKey = (bibleReading.LanguageCode, bibleReading.PublicationCode);
            if (lookupData.Translations.TryGetValue(translationKey, out var translation) &&
                !string.IsNullOrWhiteSpace(translation.Name))
            {
                scheduleStateItem.BibleReadingPublicationName = translation.Name;
                Log.Logger.Debug("Set BibleReadingPublicationName '{BibleReadingPublicationName}' for schedule {ScheduleId} (PublicationCode: {PublicationCode})",
                    translation.Name, schedule.Id, bibleReading.PublicationCode);
            }
        }

        // Use cached book name
        if (bibleReading.BookNumber > 0 &&
            !string.IsNullOrWhiteSpace(bibleReading.LanguageCode) &&
            !string.IsNullOrWhiteSpace(bibleReading.PublicationCode))
        {
            var bookKey = (bibleReading.LanguageCode, bibleReading.PublicationCode, bibleReading.BookNumber);
            if (lookupData.Books.TryGetValue(bookKey, out var bookName))
            {
                scheduleStateItem.BibleReadingBookName = bookName;
                Log.Logger.Debug("Set BibleReadingBookName '{BibleReadingBookName}' for schedule {ScheduleId} (BookNumber: {BookNumber})",
                    bookName, schedule.Id, bibleReading.BookNumber);
            }
        }
    }

    private void PopulateMusicDisplayNamesFromCache(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        LookupData lookupData)
    {
        if (schedule.Music == null)
        {
            return;
        }

        var music = schedule.Music;

        // Use cached vocal languages
        if (music.MusicType == Shared.Models.Enums.MusicType.Vocals &&
            !string.IsNullOrWhiteSpace(music.LanguageCode))
        {
            if (lookupData.VocalLanguages.TryGetValue(music.LanguageCode, out var vocalLanguage))
            {
                scheduleStateItem.MusicLanguageName = vocalLanguage.Name;
                Log.Logger.Debug("Set MusicLanguageName '{MusicLanguageName}' for schedule {ScheduleId} (LanguageCode: {LanguageCode})",
                    vocalLanguage.Name, schedule.Id, music.LanguageCode);
            }
            else
            {
                scheduleStateItem.MusicLanguageName = music.LanguageCode;
            }

            // Use cached vocal releases
            if (!string.IsNullOrWhiteSpace(music.PublicationCode))
            {
                var releaseKey = (music.LanguageCode, music.PublicationCode);
                if (lookupData.VocalReleases.TryGetValue(releaseKey, out var release))
                {
                    scheduleStateItem.MusicPublicationName = release.Name;
                    Log.Logger.Debug("Set MusicPublicationName '{MusicPublicationName}' for schedule {ScheduleId} (PublicationCode: {PublicationCode})",
                        release.Name, schedule.Id, music.PublicationCode);
                }
            }

            // Use cached vocal tracks
            if (music.TrackNumber > 0 &&
                !string.IsNullOrWhiteSpace(music.LanguageCode) &&
                !string.IsNullOrWhiteSpace(music.PublicationCode))
            {
                var trackKey = (music.LanguageCode, music.PublicationCode);
                if (lookupData.VocalTracks.TryGetValue(trackKey, out var tracks) &&
                    tracks.TryGetValue(music.TrackNumber, out var track))
                {
                    scheduleStateItem.MusicTrackName = track.Title;
                    Log.Logger.Debug("Set MusicTrackName '{MusicTrackName}' for schedule {ScheduleId} (TrackNumber: {TrackNumber})",
                        track.Title, schedule.Id, music.TrackNumber);
                }
            }
        }
        else if (music.MusicType == Shared.Models.Enums.MusicType.Melodies &&
                 !string.IsNullOrWhiteSpace(music.PublicationCode) &&
                 music.TrackNumber > 0)
        {
            // Use cached melody tracks
            if (lookupData.MelodyTracks.TryGetValue(music.PublicationCode, out var tracks) &&
                tracks.TryGetValue(music.TrackNumber, out var track))
            {
                scheduleStateItem.MusicTrackName = $"Melody Number(s) {track.Title}";
                Log.Logger.Debug("Set MusicTrackName '{MusicTrackName}' for schedule {ScheduleId} (TrackNumber: {TrackNumber})",
                    track.Title, schedule.Id, music.TrackNumber);
            }
        }
    }

    private static void SetBibleReadingLanguageName(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        BibleReadingSchedule bibleReading,
        Dictionary<string, Language>? languagesDict)
    {
        if (languagesDict == null)
        {
            return;
        }

        var languageCode = bibleReading.LanguageCode;
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return;
        }

        if (languagesDict.TryGetValue(languageCode, out var language))
        {
            scheduleStateItem.BibleReadingLanguageName = language.Name;
            Log.Logger.Debug("Set BibleReadingLanguageName '{BibleReadingLanguageName}' for schedule {ScheduleId} (LanguageCode: {LanguageCode})",
                language.Name, schedule.Id, languageCode);
        }
        else
        {
            scheduleStateItem.BibleReadingLanguageName = languageCode;
            Log.Logger.Debug("Language not found for LanguageCode '{LanguageCode}', using code as BibleReadingLanguageName for schedule {ScheduleId}",
                languageCode, schedule.Id);
        }
    }

    private async Task PopulateDefaultMusicBatchAsync(
        List<AlarmSchedule> alarmSchedules,
        ScheduleStateItem[] scheduleStateItems)
    {
        // Identify schedules that need default music
        var schedulesNeedingMusic = new List<(AlarmSchedule Schedule, ScheduleStateItem StateItem)>();
        for (int i = 0; i < alarmSchedules.Count && i < scheduleStateItems.Length; i++)
        {
            var stateItem = scheduleStateItems[i];
            if (!stateItem.MusicType.HasValue ||
                !stateItem.MusicTrackNumber.HasValue ||
                stateItem.MusicTrackNumber.Value <= 0)
            {
                schedulesNeedingMusic.Add((alarmSchedules[i], stateItem));
            }
        }

        if (schedulesNeedingMusic.Count == 0)
        {
            return;
        }

        try
        {
            const string defaultPublicationCode = "iam";

            if (melodyMusicService == null)
            {
                return;
            }

            // Load default music once for all schedules
            var melodyMusic = await melodyMusicService.GetByCodeWithTracksAsync(defaultPublicationCode);

            if (melodyMusic?.Tracks == null || melodyMusic.Tracks.Count == 0)
            {
                Log.Logger.Warning("Melody music '{PublicationCode}' not found or has no tracks - cannot populate default music for {Count} schedules",
                    defaultPublicationCode, schedulesNeedingMusic.Count);
                return;
            }

            // Apply to all schedules needing music
            var random = new Random();
            foreach (var (schedule, stateItem) in schedulesNeedingMusic)
            {
                var randomTrack = melodyMusic.Tracks[random.Next(melodyMusic.Tracks.Count)];

                stateItem.MusicType = Shared.Models.Enums.MusicType.Melodies;
                stateItem.MusicPublicationCode = defaultPublicationCode;
                stateItem.MusicLanguageCode = null;
                stateItem.MusicTrackNumber = randomTrack.Number;
                stateItem.MusicRepeat = false;
                stateItem.MusicTrackName = $"Melody Number(s) {randomTrack.Title}";

                Log.Logger.Debug("Populated default music properties for schedule {ScheduleId}. MusicType=Melodies, PublicationCode={PublicationCode}, TrackNumber={TrackNumber}",
                    schedule.Id, defaultPublicationCode, randomTrack.Number);
            }

            Log.Logger.Information("Batch populated default music for {Count} schedules", schedulesNeedingMusic.Count);
        }
        catch (Exception defaultMusicEx)
        {
            Log.Logger.Warning(defaultMusicEx, "Error batch populating default music properties for {Count} schedules",
                schedulesNeedingMusic.Count);
        }
    }

    /// <summary>
    /// Lookup data structure for batch-loaded display names.
    /// </summary>
    private sealed record LookupData(
        Dictionary<(string LanguageCode, string PublicationCode), BibleTranslation> Translations,
        Dictionary<(string LanguageCode, string PublicationCode, int BookNumber), string> Books,
        Dictionary<string, Language> VocalLanguages,
        Dictionary<(string LanguageCode, string PublicationCode), VocalMusic> VocalReleases,
        Dictionary<(string LanguageCode, string PublicationCode), SortedDictionary<int, MusicTrack>> VocalTracks,
        Dictionary<string, SortedDictionary<int, MusicTrack>> MelodyTracks);
}

