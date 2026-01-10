#nullable enable
using Bible;
using Bible.Alarm.Common;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Models;
using Serilog;

namespace Bible.Alarm.Stores.Effects.Services;

/// <summary>
/// Handles population of display names for schedule state items.
/// Separated from ScheduleEffects for better modularity.
/// </summary>
public sealed class ScheduleDisplayNamePopulator
{
    private readonly IBiblePublicationService? BiblePublicationService;
    private readonly IBiblePublicationSectionService? biblePublicationSectionService;
    private readonly IMediaService? mediaService;

    public ScheduleDisplayNamePopulator(
        IBiblePublicationService? BiblePublicationService = null,
        IBiblePublicationSectionService? biblePublicationSectionService = null,
        IMediaService? mediaService = null)
    {
        this.BiblePublicationService = BiblePublicationService ?? ServiceProviderManager.GetService<IBiblePublicationService>();
        this.biblePublicationSectionService = biblePublicationSectionService ?? ServiceProviderManager.GetService<IBiblePublicationSectionService>();
        this.mediaService = mediaService ?? ServiceProviderManager.GetService<IMediaService>();
    }

    /// <summary>
    /// Populate BiblePublicationLanguageName from language dictionary if BiblePublicationSchedule exists.
    /// </summary>
    public async Task PopulateTranslationNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        if (schedule.BiblePublicationSchedule == null || BiblePublicationService == null)
        {
            return;
        }

        try
        {
            var languageCode = schedule.BiblePublicationSchedule.LanguageCode;
            if (string.IsNullOrWhiteSpace(languageCode))
            {
                return;
            }

            var languagesDict = await BiblePublicationService.GetDistinctLanguagesAsync();
            if (languagesDict.TryGetValue(languageCode, out var language))
            {
                scheduleStateItem.BiblePublicationLanguageName = language.Name;
                Log.Debug("ScheduleEffects: Set BiblePublicationLanguageName '{BiblePublicationLanguageName}' for schedule {ScheduleId} (LanguageCode: {LanguageCode})",
                    language.Name, schedule.Id, languageCode);
            }
            else
            {
                scheduleStateItem.BiblePublicationLanguageName = languageCode;
                Log.Debug("ScheduleEffects: Language not found for LanguageCode '{LanguageCode}', using code as BiblePublicationLanguageName for schedule {ScheduleId}",
                    languageCode, schedule.Id);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating BiblePublicationLanguageName for schedule {ScheduleId}", schedule.Id);
            // Fallback to language code
            scheduleStateItem.BiblePublicationLanguageName = schedule.BiblePublicationSchedule.LanguageCode;
        }
    }

    /// <summary>
    /// Populate BiblePublicationLanguageName from language dictionary using language code from ScheduleStateItem.
    /// </summary>
    public async Task PopulateTranslationNameAsync(ScheduleStateItem scheduleStateItem)
    {
        if (string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationLanguageCode) || BiblePublicationService == null)
        {
            return;
        }

        try
        {
            var languagesDict = await BiblePublicationService.GetDistinctLanguagesAsync();
            if (languagesDict.TryGetValue(scheduleStateItem.BiblePublicationLanguageCode, out var language))
            {
                scheduleStateItem.BiblePublicationLanguageName = language.Name;
                Log.Debug("ScheduleEffects: Set BiblePublicationLanguageName '{BiblePublicationLanguageName}' for schedule {ScheduleId} (LanguageCode: {LanguageCode})",
                    language.Name, scheduleStateItem.Id, scheduleStateItem.BiblePublicationLanguageCode);
            }
            else
            {
                scheduleStateItem.BiblePublicationLanguageName = scheduleStateItem.BiblePublicationLanguageCode;
                Log.Debug("ScheduleEffects: Language not found for LanguageCode '{LanguageCode}', using code as BiblePublicationLanguageName for schedule {ScheduleId}",
                    scheduleStateItem.BiblePublicationLanguageCode, scheduleStateItem.Id);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating BiblePublicationLanguageName for schedule {ScheduleId}", scheduleStateItem.Id);
            // Fallback to language code
            scheduleStateItem.BiblePublicationLanguageName = scheduleStateItem.BiblePublicationLanguageCode;
        }
    }

    /// <summary>
    /// Populate BiblePublicationName from BiblePublicationService if BiblePublicationSchedule exists.
    /// </summary>
    public async Task PopulatePublicationNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        if (schedule.BiblePublicationSchedule == null || BiblePublicationService == null)
        {
            return;
        }

        try
        {
            var biblePublication = schedule.BiblePublicationSchedule;
            if (string.IsNullOrWhiteSpace(biblePublication.LanguageCode) ||
                string.IsNullOrWhiteSpace(biblePublication.PublicationCode))
            {
                return;
            }

            var translation = await BiblePublicationService.GetByLanguageAndCodeWithSectionsAsync(
                biblePublication.LanguageCode,
                biblePublication.PublicationCode);

            if (translation != null && !string.IsNullOrWhiteSpace(translation.Name))
            {
                scheduleStateItem.BiblePublicationName = translation.Name;
                Log.Debug("ScheduleEffects: Set BiblePublicationName '{BiblePublicationName}' for schedule {ScheduleId} (PublicationCode: {PublicationCode})",
                    translation.Name, schedule.Id, biblePublication.PublicationCode);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating BiblePublicationName for schedule {ScheduleId}", schedule.Id);
        }
    }

    /// <summary>
    /// Populate SectionName from BiblePublicationSectionService if BiblePublicationSchedule exists.
    /// </summary>
    public async Task PopulateSectionNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        if (schedule.BiblePublicationSchedule == null || biblePublicationSectionService == null)
        {
            return;
        }

        try
        {
            var biblePublication = schedule.BiblePublicationSchedule;
            if (!biblePublication.SectionNumber.HasValue || biblePublication.SectionNumber.Value <= 0 ||
                string.IsNullOrWhiteSpace(biblePublication.LanguageCode) ||
                string.IsNullOrWhiteSpace(biblePublication.PublicationCode))
            {
                return;
            }

            var sectionName = await biblePublicationSectionService.GetSectionNameAsync(
                biblePublication.LanguageCode,
                biblePublication.PublicationCode,
                biblePublication.SectionNumber.Value);

            if (!string.IsNullOrWhiteSpace(sectionName))
            {
                scheduleStateItem.BiblePublicationSectionName = sectionName;
                Log.Debug("ScheduleEffects: Set BiblePublicationSectionName '{BiblePublicationSectionName}' for schedule {ScheduleId} (SectionNumber: {SectionNumber})",
                    sectionName, schedule.Id, biblePublication.SectionNumber);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating SectionName for schedule {ScheduleId}", schedule.Id);
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

