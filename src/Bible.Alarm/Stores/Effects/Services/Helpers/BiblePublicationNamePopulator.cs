#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Models;
using Serilog;

namespace Bible.Alarm.Stores.Effects.Services.Helpers;

/// <summary>
/// Helper class for populating Bible publication related display names.
/// </summary>
internal sealed class BiblePublicationNamePopulator
{
    private readonly IBiblePublicationService? biblePublicationService;
    private readonly IBiblePublicationSectionService? biblePublicationSectionService;
    private readonly IMediaService? mediaService;

    public BiblePublicationNamePopulator(
        IBiblePublicationService? biblePublicationService,
        IBiblePublicationSectionService? biblePublicationSectionService,
        IMediaService? mediaService)
    {
        this.biblePublicationService = biblePublicationService;
        this.biblePublicationSectionService = biblePublicationSectionService;
        this.mediaService = mediaService;
    }

    /// <summary>
    /// Populate BiblePublicationLanguageName from language dictionary if BiblePublicationSchedule exists.
    /// </summary>
    public async Task PopulatePublicationNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        if (schedule.BiblePublicationSchedule == null || biblePublicationService == null)
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

            var languagesDict = await biblePublicationService.GetDistinctLanguagesAsync();
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
        if (string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationLanguageCode) || biblePublicationService == null)
        {
            return;
        }

        try
        {
            var languagesDict = await biblePublicationService.GetDistinctLanguagesAsync();
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
        if (schedule.BiblePublicationSchedule == null || biblePublicationService == null)
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

            var publication = await biblePublicationService.GetByLanguageAndCodeWithSectionsAsync(
                biblePublication.LanguageCode,
                biblePublication.PublicationCode);

            if (publication != null)
            {
                if (!string.IsNullOrWhiteSpace(publication.Name))
                {
                    scheduleStateItem.BiblePublicationName = publication.Name;
                    Log.Debug("ScheduleEffects: Set BiblePublicationName '{BiblePublicationName}' for schedule {ScheduleId} (PublicationCode: {PublicationCode})",
                        publication.Name, schedule.Id, biblePublication.PublicationCode);
                }

                // Populate category from publication
                if (publication.Category != null)
                {
                    scheduleStateItem.BiblePublicationCategoryId = publication.CategoryId;
                    scheduleStateItem.BiblePublicationCategoryName = publication.Category.CategoryName;
                    Log.Debug("ScheduleEffects: Set BiblePublicationCategoryId={CategoryId}, BiblePublicationCategoryName='{CategoryName}' for schedule {ScheduleId} (PublicationCode: {PublicationCode})",
                        publication.CategoryId, publication.Category.CategoryName, schedule.Id, biblePublication.PublicationCode);
                }
                else
                {
                    // Fallback: derive category name from publication code
                    var categoryName = JwSourceHelper.GetCategoryName(biblePublication.PublicationCode);
                    if (!string.IsNullOrWhiteSpace(categoryName))
                    {
                        scheduleStateItem.BiblePublicationCategoryName = categoryName;
                        Log.Debug("ScheduleEffects: Set BiblePublicationCategoryName='{CategoryName}' from publication code for schedule {ScheduleId} (PublicationCode: {PublicationCode})",
                            categoryName, schedule.Id, biblePublication.PublicationCode);
                    }
                }
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
            var sectionNumber = await SectionCodeConverter.ConvertToIntAsync(
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
        if (schedule.BiblePublicationSchedule == null || biblePublicationService == null)
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
        var sectionNumber = await SectionCodeConverter.ConvertToIntAsync(
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
        var publication = await biblePublicationService!.GetByLanguageAndCodeWithTracksAsync(
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

            // Populate category from publication (for non-sectioned publications that weren't loaded earlier)
            if (publication.Category != null && 
                (scheduleStateItem.BiblePublicationCategoryId == null || string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationCategoryName)))
            {
                scheduleStateItem.BiblePublicationCategoryId = publication.CategoryId;
                scheduleStateItem.BiblePublicationCategoryName = publication.Category.CategoryName;
                Log.Debug("ScheduleEffects: Set BiblePublicationCategoryId={CategoryId}, BiblePublicationCategoryName='{CategoryName}' for non-sectioned publication in schedule {ScheduleId}",
                    publication.CategoryId, publication.Category.CategoryName, scheduleId);
            }
        }
    }
}
