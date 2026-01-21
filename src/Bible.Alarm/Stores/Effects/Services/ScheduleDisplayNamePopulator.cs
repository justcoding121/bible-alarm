#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
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
    public async Task PopulatePublicationNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
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
                scheduleStateItem.BiblePublicationLanguageDirection = language.Direction;
                Log.Debug("ScheduleEffects: Set BiblePublicationLanguageName '{BiblePublicationLanguageName}' and Direction '{Direction}' for schedule {ScheduleId} (LanguageCode: {LanguageCode})",
                    language.Name, language.Direction, schedule.Id, languageCode);
            }
            else
            {
                scheduleStateItem.BiblePublicationLanguageName = languageCode;
                scheduleStateItem.BiblePublicationLanguageDirection = "ltr"; // Default to LTR
                Log.Debug("ScheduleEffects: Language not found for LanguageCode '{LanguageCode}', using code as BiblePublicationLanguageName for schedule {ScheduleId}",
                    languageCode, schedule.Id);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating BiblePublicationLanguageName for schedule {ScheduleId}", schedule.Id);
            // Fallback to language code
            scheduleStateItem.BiblePublicationLanguageName = schedule.BiblePublicationSchedule.LanguageCode;
            scheduleStateItem.BiblePublicationLanguageDirection = "ltr"; // Default to LTR
        }
    }

    /// <summary>
    /// Populate BiblePublicationLanguageName from language dictionary using language code from ScheduleStateItem.
    /// </summary>
    public async Task PopulatePublicationNameAsync(ScheduleStateItem scheduleStateItem)
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
                scheduleStateItem.BiblePublicationLanguageDirection = language.Direction;
                Log.Debug("ScheduleEffects: Set BiblePublicationLanguageName '{BiblePublicationLanguageName}' and Direction '{Direction}' for schedule {ScheduleId} (LanguageCode: {LanguageCode})",
                    language.Name, language.Direction, scheduleStateItem.Id, scheduleStateItem.BiblePublicationLanguageCode);
            }
            else
            {
                scheduleStateItem.BiblePublicationLanguageName = scheduleStateItem.BiblePublicationLanguageCode;
                scheduleStateItem.BiblePublicationLanguageDirection = "ltr"; // Default to LTR
                Log.Debug("ScheduleEffects: Language not found for LanguageCode '{LanguageCode}', using code as BiblePublicationLanguageName for schedule {ScheduleId}",
                    scheduleStateItem.BiblePublicationLanguageCode, scheduleStateItem.Id);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating BiblePublicationLanguageName for schedule {ScheduleId}", scheduleStateItem.Id);
            // Fallback to language code
            scheduleStateItem.BiblePublicationLanguageName = scheduleStateItem.BiblePublicationLanguageCode;
            scheduleStateItem.BiblePublicationLanguageDirection = "ltr"; // Default to LTR
        }
    }

    /// <summary>
    /// Populate BiblePublicationName from BiblePublicationService if BiblePublicationSchedule exists.
    /// </summary>
    public async Task PopulateBiblePublicationNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
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

            var publication = await BiblePublicationService.GetByLanguageAndCodeWithSectionsAsync(
                biblePublication.LanguageCode,
                biblePublication.PublicationCode);

            if (publication != null && !string.IsNullOrWhiteSpace(publication.Name))
            {
                scheduleStateItem.BiblePublicationName = publication.Name;
                Log.Debug("ScheduleEffects: Set BiblePublicationName '{BiblePublicationName}' for schedule {ScheduleId} (PublicationCode: {PublicationCode})",
                    publication.Name, schedule.Id, biblePublication.PublicationCode);
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
            var sectionNumber = await ConvertSectionCodeToIntAsync(
                biblePublication.SectionCode,
                biblePublication.LanguageCode,
                biblePublication.PublicationCode);
            
            if (sectionNumber <= 0 ||
                string.IsNullOrWhiteSpace(biblePublication.LanguageCode) ||
                string.IsNullOrWhiteSpace(biblePublication.PublicationCode))
            {
                return;
            }

            var sectionName = await biblePublicationSectionService.GetSectionNameAsync(
                biblePublication.LanguageCode,
                biblePublication.PublicationCode,
                sectionNumber);

            if (!string.IsNullOrWhiteSpace(sectionName))
            {
                scheduleStateItem.BiblePublicationSectionName = sectionName;
                Log.Debug("ScheduleEffects: Set BiblePublicationSectionName '{BiblePublicationSectionName}' for schedule {ScheduleId} (SectionCode: {SectionCode})",
                    sectionName, schedule.Id, biblePublication.SectionCode);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating SectionName for schedule {ScheduleId}", schedule.Id);
        }
    }

    /// <summary>
    /// Populate TrackTitle for all Bible publications from BiblePublicationService.
    /// For sectioned publications (traditional Bible), loads track from section.
    /// For non-sectioned publications (drama/video), loads track directly from publication.
    /// </summary>
    public async Task PopulateTrackTitleAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        if (schedule.BiblePublicationSchedule == null || BiblePublicationService == null)
        {
            return;
        }

        try
        {
            var biblePublication = schedule.BiblePublicationSchedule;

            if (biblePublication.TrackNumber <= 0 ||
                string.IsNullOrWhiteSpace(biblePublication.LanguageCode) ||
                string.IsNullOrWhiteSpace(biblePublication.PublicationCode))
            {
                return;
            }

            if (PublicationTypeHelper.HasSectionStructure(biblePublication.PublicationCode))
            {
                // Sectioned publications (traditional Bible) - load track from section
                await PopulateTrackTitleFromSectionAsync(scheduleStateItem, biblePublication, schedule.Id);
            }
            else
            {
                // Non-sectioned publications (drama/video) - load track directly from publication
                await PopulateTrackTitleFromPublicationAsync(scheduleStateItem, biblePublication, schedule.Id);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating TrackTitle for schedule {ScheduleId}", schedule.Id);
        }
    }

    private async Task PopulateTrackTitleFromSectionAsync(
        ScheduleStateItem scheduleStateItem,
        BiblePublicationSchedule biblePublication,
        int scheduleId)
    {
        var sectionNumber = await ConvertSectionCodeToIntAsync(
            biblePublication.SectionCode,
            biblePublication.LanguageCode,
            biblePublication.PublicationCode);
        
        if (sectionNumber <= 0 || mediaService == null)
        {
            return;
        }

        var tracks = await mediaService.GetBiblePublicationTracks(
            biblePublication.LanguageCode,
            biblePublication.PublicationCode,
            sectionNumber);

        if (tracks != null && tracks.TryGetValue(biblePublication.TrackNumber, out var track))
        {
            if (!string.IsNullOrWhiteSpace(track.Title))
            {
                scheduleStateItem.BiblePublicationTrackTitle = track.Title;
                Log.Debug("ScheduleEffects: Set BiblePublicationTrackTitle '{BiblePublicationTrackTitle}' for schedule {ScheduleId} (SectionCode: {SectionCode}, TrackNumber: {TrackNumber})",
                    track.Title, scheduleId, biblePublication.SectionCode, biblePublication.TrackNumber);
            }
        }
    }

    private async Task PopulateTrackTitleFromPublicationAsync(
        ScheduleStateItem scheduleStateItem,
        BiblePublicationSchedule biblePublication,
        int scheduleId)
    {
        // Use GetByLanguageAndCodeWithTracksAsync to load tracks for drama/video publications
        var publication = await BiblePublicationService!.GetByLanguageAndCodeWithTracksAsync(
            biblePublication.LanguageCode,
            biblePublication.PublicationCode);

        if (publication != null)
        {
            var track = publication.Tracks.FirstOrDefault(t => t.Number == biblePublication.TrackNumber);
            if (track != null && !string.IsNullOrWhiteSpace(track.Title))
            {
                scheduleStateItem.BiblePublicationTrackTitle = track.Title;
                Log.Debug("ScheduleEffects: Set BiblePublicationTrackTitle '{BiblePublicationTrackTitle}' for schedule {ScheduleId} (TrackNumber: {TrackNumber})",
                    track.Title, scheduleId, biblePublication.TrackNumber);
            }
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
            if (music.MusicType != MusicType.VocalMusic ||
                string.IsNullOrWhiteSpace(music.LanguageCode))
            {
                return;
            }

            var languagesDict = await mediaService.GetVocalMusicLanguages();
            if (languagesDict.TryGetValue(music.LanguageCode, out var language))
            {
                scheduleStateItem.MusicLanguageName = language.Name;
                scheduleStateItem.MusicLanguageDirection = language.Direction;
                Log.Debug("ScheduleEffects: Set MusicLanguageName '{MusicLanguageName}' and MusicLanguageDirection '{MusicLanguageDirection}' for schedule {ScheduleId} (LanguageCode: {LanguageCode})",
                    language.Name, language.Direction, schedule.Id, music.LanguageCode);
            }
            else
            {
                scheduleStateItem.MusicLanguageName = music.LanguageCode;
                scheduleStateItem.MusicLanguageDirection = "ltr"; // Default to LTR if language not found
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
                scheduleStateItem.MusicLanguageDirection = "ltr"; // Default to LTR on error
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
            if (music.MusicType != MusicType.VocalMusic ||
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
            if (music.MusicType == MusicType.Music)
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
            else if (music.MusicType == MusicType.VocalMusic)
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

    /// <summary>
    /// Converts SectionCode (string) to the int section number needed for media service calls.
    /// Tries to parse SectionCode to int, or finds the section by SectionCode and uses its BookNum.
    /// Returns 0 for null/empty (non-sectioned publications).
    /// </summary>
    private async Task<int> ConvertSectionCodeToIntAsync(string? sectionCode, string languageCode, string publicationCode)
    {
        if (string.IsNullOrEmpty(sectionCode))
        {
            return 0;
        }

        // Try to parse SectionCode directly to int
        if (int.TryParse(sectionCode, out var sectionNumber))
        {
            return sectionNumber;
        }

        // If parsing fails, find the section by SectionCode and use its BookNum
        if (mediaService != null)
        {
            try
            {
                var sections = await mediaService.GetBiblePublicationSections(languageCode, publicationCode);
                var section = sections.Values.FirstOrDefault(s => s.SectionCode.Equals(sectionCode, StringComparison.OrdinalIgnoreCase));
                if (section != null && section.BookNum.HasValue)
                {
                    return section.BookNum.Value;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error converting SectionCode {SectionCode} to int for {LanguageCode}/{PublicationCode}",
                    sectionCode, languageCode, publicationCode);
            }
        }

        // Fallback: return 0 if section not found
        Log.Warning("Could not convert SectionCode {SectionCode} to int for {LanguageCode}/{PublicationCode}, using 0",
            sectionCode, languageCode, publicationCode);
        return 0;
    }
}

