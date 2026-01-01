#nullable enable
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Models;
using Serilog;

namespace Bible.Alarm.Stores.Effects;

/// <summary>
/// Handles population of display names for schedule state items.
/// Separated from ScheduleEffects for better modularity.
/// </summary>
public sealed class ScheduleDisplayNamePopulator
{
    private readonly IBibleTranslationService? bibleTranslationService;
    private readonly IBibleBookService? bibleBookService;
    private readonly IMediaService? mediaService;

    public ScheduleDisplayNamePopulator(
        IBibleTranslationService? bibleTranslationService = null,
        IBibleBookService? bibleBookService = null,
        IMediaService? mediaService = null)
    {
        this.bibleTranslationService = bibleTranslationService ?? ServiceProviderManager.GetService<IBibleTranslationService>();
        this.bibleBookService = bibleBookService ?? ServiceProviderManager.GetService<IBibleBookService>();
        this.mediaService = mediaService ?? ServiceProviderManager.GetService<IMediaService>();
    }

    /// <summary>
    /// Populate BibleReadingLanguageName from language dictionary if BibleReadingSchedule exists.
    /// </summary>
    public async Task PopulateTranslationNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        if (schedule.BibleReadingSchedule == null || bibleTranslationService == null)
        {
            return;
        }

        try
        {
            var languageCode = schedule.BibleReadingSchedule.LanguageCode;
            if (string.IsNullOrWhiteSpace(languageCode))
            {
                return;
            }

            var languagesDict = await bibleTranslationService.GetDistinctLanguagesAsync();
            if (languagesDict.TryGetValue(languageCode, out var language))
            {
                scheduleStateItem.BibleReadingLanguageName = language.Name;
                Log.Debug("ScheduleEffects: Set BibleReadingLanguageName '{BibleReadingLanguageName}' for schedule {ScheduleId} (LanguageCode: {LanguageCode})",
                    language.Name, schedule.Id, languageCode);
            }
            else
            {
                scheduleStateItem.BibleReadingLanguageName = languageCode;
                Log.Debug("ScheduleEffects: Language not found for LanguageCode '{LanguageCode}', using code as BibleReadingLanguageName for schedule {ScheduleId}",
                    languageCode, schedule.Id);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating BibleReadingLanguageName for schedule {ScheduleId}", schedule.Id);
            // Fallback to language code
            scheduleStateItem.BibleReadingLanguageName = schedule.BibleReadingSchedule.LanguageCode;
        }
    }

    /// <summary>
    /// Populate BibleReadingLanguageName from language dictionary using language code from ScheduleStateItem.
    /// </summary>
    public async Task PopulateTranslationNameAsync(ScheduleStateItem scheduleStateItem)
    {
        if (string.IsNullOrWhiteSpace(scheduleStateItem.BibleReadingLanguageCode) || bibleTranslationService == null)
        {
            return;
        }

        try
        {
            var languagesDict = await bibleTranslationService.GetDistinctLanguagesAsync();
            if (languagesDict.TryGetValue(scheduleStateItem.BibleReadingLanguageCode, out var language))
            {
                scheduleStateItem.BibleReadingLanguageName = language.Name;
                Log.Debug("ScheduleEffects: Set BibleReadingLanguageName '{BibleReadingLanguageName}' for schedule {ScheduleId} (LanguageCode: {LanguageCode})",
                    language.Name, scheduleStateItem.Id, scheduleStateItem.BibleReadingLanguageCode);
            }
            else
            {
                scheduleStateItem.BibleReadingLanguageName = scheduleStateItem.BibleReadingLanguageCode;
                Log.Debug("ScheduleEffects: Language not found for LanguageCode '{LanguageCode}', using code as BibleReadingLanguageName for schedule {ScheduleId}",
                    scheduleStateItem.BibleReadingLanguageCode, scheduleStateItem.Id);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating BibleReadingLanguageName for schedule {ScheduleId}", scheduleStateItem.Id);
            // Fallback to language code
            scheduleStateItem.BibleReadingLanguageName = scheduleStateItem.BibleReadingLanguageCode;
        }
    }

    /// <summary>
    /// Populate BibleReadingPublicationName from BibleTranslationService if BibleReadingSchedule exists.
    /// </summary>
    public async Task PopulatePublicationNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        if (schedule.BibleReadingSchedule == null || bibleTranslationService == null)
        {
            return;
        }

        try
        {
            var bibleReading = schedule.BibleReadingSchedule;
            if (string.IsNullOrWhiteSpace(bibleReading.LanguageCode) ||
                string.IsNullOrWhiteSpace(bibleReading.PublicationCode))
            {
                return;
            }

            var translation = await bibleTranslationService.GetByLanguageAndCodeWithBooksAsync(
                bibleReading.LanguageCode,
                bibleReading.PublicationCode);

            if (translation != null && !string.IsNullOrWhiteSpace(translation.Name))
            {
                scheduleStateItem.BibleReadingPublicationName = translation.Name;
                Log.Debug("ScheduleEffects: Set BibleReadingPublicationName '{BibleReadingPublicationName}' for schedule {ScheduleId} (PublicationCode: {PublicationCode})",
                    translation.Name, schedule.Id, bibleReading.PublicationCode);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating BibleReadingPublicationName for schedule {ScheduleId}", schedule.Id);
        }
    }

    /// <summary>
    /// Populate BookName from BibleBookService if BibleReadingSchedule exists.
    /// </summary>
    public async Task PopulateBookNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        if (schedule.BibleReadingSchedule == null || bibleBookService == null)
        {
            return;
        }

        try
        {
            var bibleReading = schedule.BibleReadingSchedule;
            if (bibleReading.BookNumber <= 0 ||
                string.IsNullOrWhiteSpace(bibleReading.LanguageCode) ||
                string.IsNullOrWhiteSpace(bibleReading.PublicationCode))
            {
                return;
            }

            var bookName = await bibleBookService.GetBookNameAsync(
                bibleReading.LanguageCode,
                bibleReading.PublicationCode,
                bibleReading.BookNumber);

            if (!string.IsNullOrWhiteSpace(bookName))
            {
                scheduleStateItem.BibleReadingBookName = bookName;
                Log.Debug("ScheduleEffects: Set BibleReadingBookName '{BibleReadingBookName}' for schedule {ScheduleId} (BookNumber: {BookNumber})",
                    bookName, schedule.Id, bibleReading.BookNumber);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating BookName for schedule {ScheduleId}", schedule.Id);
        }
    }

    /// <summary>
    /// Populate MusicLanguageName from vocal music languages if Music exists and is Vocals.
    /// </summary>
    public async Task PopulateMusicLanguageNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        if (schedule.Music == null || mediaService == null)
        {
            return;
        }

        try
        {
            var music = schedule.Music;
            // Only populate for vocals (melodies don't have language)
            if (music.MusicType != MusicType.Vocals ||
                string.IsNullOrWhiteSpace(music.LanguageCode))
            {
                return;
            }

            var languagesDict = await mediaService.GetVocalMusicLanguages();
            if (languagesDict.TryGetValue(music.LanguageCode, out var language))
            {
                scheduleStateItem.MusicLanguageName = language.Name;
                Log.Debug("ScheduleEffects: Set MusicLanguageName '{MusicLanguageName}' for schedule {ScheduleId} (LanguageCode: {LanguageCode})",
                    language.Name, schedule.Id, music.LanguageCode);
            }
            else
            {
                scheduleStateItem.MusicLanguageName = music.LanguageCode;
                Log.Debug("ScheduleEffects: Language not found for LanguageCode '{LanguageCode}', using code as MusicLanguageName for schedule {ScheduleId}",
                    music.LanguageCode, schedule.Id);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating MusicLanguageName for schedule {ScheduleId}", schedule.Id);
            if (schedule.Music != null)
            {
                scheduleStateItem.MusicLanguageName = schedule.Music.LanguageCode;
            }
        }
    }

    /// <summary>
    /// Populate MusicPublicationName from vocal music releases if Music exists and is Vocals.
    /// </summary>
    public async Task PopulateMusicPublicationNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        if (schedule.Music == null || mediaService == null)
        {
            return;
        }

        try
        {
            var music = schedule.Music;
            // Only populate for vocals (melodies don't have publication name in the same way)
            if (music.MusicType != MusicType.Vocals ||
                string.IsNullOrWhiteSpace(music.LanguageCode) ||
                string.IsNullOrWhiteSpace(music.PublicationCode))
            {
                return;
            }

            var releases = await mediaService.GetVocalMusicReleases(music.LanguageCode);
            if (releases.TryGetValue(music.PublicationCode, out var release))
            {
                scheduleStateItem.MusicPublicationName = release.Name;
                Log.Debug("ScheduleEffects: Set MusicPublicationName '{MusicPublicationName}' for schedule {ScheduleId} (PublicationCode: {PublicationCode})",
                    release.Name, schedule.Id, music.PublicationCode);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating MusicPublicationName for schedule {ScheduleId}", schedule.Id);
        }
    }

    /// <summary>
    /// Populate MusicTrackName from music tracks if Music exists.
    /// </summary>
    public async Task PopulateMusicTrackNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        if (schedule.Music == null || mediaService == null)
        {
            return;
        }

        try
        {
            var music = schedule.Music;
            if (music.TrackNumber <= 0)
            {
                return;
            }

            string? trackName = null;
            if (music.MusicType == MusicType.Melodies)
            {
                if (string.IsNullOrWhiteSpace(music.PublicationCode))
                {
                    return;
                }

                var tracks = await mediaService.GetMelodyMusicTracks(music.PublicationCode);
                if (tracks.TryGetValue(music.TrackNumber, out var track))
                {
                    // Format melody track title with prefix to match track modal display
                    trackName = $"Melody Number(s) {track.Title}";
                }
            }
            else if (music.MusicType == MusicType.Vocals)
            {
                if (string.IsNullOrWhiteSpace(music.LanguageCode) || string.IsNullOrWhiteSpace(music.PublicationCode))
                {
                    return;
                }

                var tracks = await mediaService.GetVocalMusicTracks(music.LanguageCode, music.PublicationCode);
                if (tracks.TryGetValue(music.TrackNumber, out var track))
                {
                    trackName = track.Title;
                }
            }

            if (!string.IsNullOrWhiteSpace(trackName))
            {
                scheduleStateItem.MusicTrackName = trackName;
                Log.Debug("ScheduleEffects: Set MusicTrackName '{MusicTrackName}' for schedule {ScheduleId} (TrackNumber: {TrackNumber})",
                    trackName, schedule.Id, music.TrackNumber);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating MusicTrackName for schedule {ScheduleId}", schedule.Id);
        }
    }
}

