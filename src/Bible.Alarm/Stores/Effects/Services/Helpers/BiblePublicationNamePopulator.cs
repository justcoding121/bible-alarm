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
        if (schedule.BiblePublicationSchedule == null)
        {
            return;
        }

        try
        {
            var biblePublication = schedule.BiblePublicationSchedule;
            if (string.IsNullOrWhiteSpace(biblePublication.PublicationCode))
            {
                return;
            }

            // Normal (language-bound) publications.
            if (!string.IsNullOrWhiteSpace(biblePublication.LanguageCode) && biblePublicationService != null)
            {
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

                    // Only populate category if it's not already set (initial population from DB)
                    // Category can only be changed via CategorySelectionAction, not from publication
                    if ((scheduleStateItem.BiblePublicationCategoryId == null || string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationCategoryName)))
                    {
                        if (publication.Category != null)
                        {
                            scheduleStateItem.BiblePublicationCategoryId = publication.CategoryId;
                            scheduleStateItem.BiblePublicationCategoryName = publication.Category.CategoryName;
                            Log.Debug("ScheduleEffects: Set BiblePublicationCategoryId={CategoryId}, BiblePublicationCategoryName='{CategoryName}' for schedule {ScheduleId} (PublicationCode: {PublicationCode}) - initial population",
                                publication.CategoryId, publication.Category.CategoryName, schedule.Id, biblePublication.PublicationCode);
                        }
                        else
                        {
                            // Fallback: derive category name from publication code
                            var categoryName = JwSourceHelper.GetCategoryName(biblePublication.PublicationCode);
                            if (!string.IsNullOrWhiteSpace(categoryName))
                            {
                                scheduleStateItem.BiblePublicationCategoryName = categoryName;
                                Log.Debug("ScheduleEffects: Set BiblePublicationCategoryName='{CategoryName}' from publication code for schedule {ScheduleId} (PublicationCode: {PublicationCode}) - initial population",
                                    categoryName, schedule.Id, biblePublication.PublicationCode);
                            }
                        }
                    }
                    else
                    {
                        Log.Debug("ScheduleEffects: Preserving existing category '{CategoryName}' for schedule {ScheduleId} (PublicationCode: {PublicationCode})",
                            scheduleStateItem.BiblePublicationCategoryName, schedule.Id, biblePublication.PublicationCode);
                    }
                }

                return;
            }

            // Publications without a language FK (e.g. melody music like "iam") – resolve via media index.
            if (mediaService != null)
            {
                var languageCode = biblePublication.LanguageCode ?? string.Empty;
                var pubs = await mediaService.GetBiblePublications(languageCode, categoryName: null, downloadAll: false, progress: null);
                var pub = pubs.Values.FirstOrDefault(p =>
                    p != null && string.Equals(p.PublicationCode, biblePublication.PublicationCode, StringComparison.OrdinalIgnoreCase));

                if (pub != null)
                {
                    if (string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationName) && !string.IsNullOrWhiteSpace(pub.Name))
                    {
                        scheduleStateItem.BiblePublicationName = pub.Name;
                    }

                    if ((scheduleStateItem.BiblePublicationCategoryId == null || string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationCategoryName)) &&
                        pub.Category != null)
                    {
                        scheduleStateItem.BiblePublicationCategoryId = pub.CategoryId;
                        scheduleStateItem.BiblePublicationCategoryName = pub.Category.CategoryName;
                    }
                }

                if (string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationCategoryName))
                {
                    var categoryName = JwSourceHelper.GetCategoryName(biblePublication.PublicationCode);
                    if (!string.IsNullOrWhiteSpace(categoryName))
                    {
                        scheduleStateItem.BiblePublicationCategoryName = categoryName;
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
        if (schedule.BiblePublicationSchedule == null)
        {
            return;
        }

        try
        {
            var biblePublication = schedule.BiblePublicationSchedule;
            var sectionCode = await SectionCodeConverter.ConvertToIntAsync(
                biblePublication.SectionCode,
                biblePublication.LanguageCode,
                biblePublication.PublicationCode);
            
            if (sectionCode <= 0 ||
                string.IsNullOrWhiteSpace(biblePublication.PublicationCode))
            {
                return;
            }

            // Prefer schedule DB SectionCode exact match when resolving via media service.
            var normalizedSectionCode = SectionCodeHelper.Normalize(biblePublication.SectionCode);

            // Language-bound section service.
            if (!string.IsNullOrWhiteSpace(biblePublication.LanguageCode) && biblePublicationSectionService != null)
            {
                var sectionName = await biblePublicationSectionService.GetSectionNameAsync(
                    biblePublication.LanguageCode,
                    biblePublication.PublicationCode,
                    sectionCode);

                if (!string.IsNullOrWhiteSpace(sectionName))
                {
                    scheduleStateItem.BiblePublicationSectionName = sectionName;
                    Log.Debug("ScheduleEffects: Set BiblePublicationSectionName '{BiblePublicationSectionName}' for schedule {ScheduleId} (SectionCode: {SectionCode})",
                        sectionName, schedule.Id, biblePublication.SectionCode);
                }

                return;
            }

            // No-language publications: resolve section name from media index.
            if (mediaService != null)
            {
                var languageCode = biblePublication.LanguageCode ?? string.Empty;
                var sections = await mediaService.GetBiblePublicationSections(languageCode, biblePublication.PublicationCode);
                if (sections != null && sections.Count > 0)
                {
                    var match = sections.Values.FirstOrDefault(s =>
                        s != null &&
                        !string.IsNullOrWhiteSpace(s.SectionCode) &&
                        normalizedSectionCode != null &&
                        string.Equals(s.SectionCode, normalizedSectionCode, StringComparison.OrdinalIgnoreCase));

                    if (match != null && !string.IsNullOrWhiteSpace(match.Name))
                    {
                        scheduleStateItem.BiblePublicationSectionName = match.Name;
                        return;
                    }

                    if (sections.TryGetValue(sectionCode, out var sectionByIndex) &&
                        sectionByIndex != null &&
                        !string.IsNullOrWhiteSpace(sectionByIndex.Name))
                    {
                        scheduleStateItem.BiblePublicationSectionName = sectionByIndex.Name;
                    }
                }
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
        if (schedule.BiblePublicationSchedule == null)
        {
            return;
        }

        try
        {
            var biblePublication = schedule.BiblePublicationSchedule;
            var languageCode = biblePublication.LanguageCode ?? string.Empty;
            var publicationCode = biblePublication.PublicationCode ?? string.Empty;

            if (biblePublication.TrackNumber <= 0 || string.IsNullOrWhiteSpace(publicationCode))
            {
                return;
            }

            if (PublicationTypeHelper.HasSectionStructure(publicationCode))
            {
                // Sectioned publications (traditional Bible) - load track from section
                await PopulateTrackTitleFromSectionAsync(scheduleStateItem, languageCode, publicationCode, biblePublication.SectionCode, biblePublication.TrackNumber, schedule.Id);
            }
            else
            {
                // Non-sectioned publications (drama/video) - load track directly from publication
                if (!string.IsNullOrWhiteSpace(languageCode) && biblePublicationService != null)
                {
                    await PopulateTrackTitleFromPublicationAsync(scheduleStateItem, biblePublication, schedule.Id);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating TrackTitle for schedule {ScheduleId}", schedule.Id);
        }
    }

    private async Task PopulateTrackTitleFromSectionAsync(
        ScheduleStateItem scheduleStateItem,
        string languageCode,
        string publicationCode,
        string? sectionCodeString,
        int trackNumber,
        int scheduleId)
    {
        var sectionCode = await SectionCodeConverter.ConvertToIntAsync(sectionCodeString, languageCode, publicationCode);
        
        if (sectionCode <= 0 || mediaService == null)
        {
            return;
        }

        var tracks = await mediaService.GetBiblePublicationTracks(
            languageCode,
            publicationCode,
            sectionCode);

        if (tracks != null && tracks.TryGetValue(trackNumber, out var track))
        {
            if (!string.IsNullOrWhiteSpace(track.Title))
            {
                scheduleStateItem.BiblePublicationTrackTitle = track.Title;
                Log.Debug("ScheduleEffects: Set BiblePublicationTrackTitle '{BiblePublicationTrackTitle}' for schedule {ScheduleId} (SectionCode: {SectionCode}, TrackNumber: {TrackNumber})",
                    track.Title, scheduleId, sectionCodeString, trackNumber);
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

            // Only populate category if it's not already set (initial population from DB)
            // Category can only be changed via CategorySelectionAction, not from publication
            if (publication.Category != null && 
                (scheduleStateItem.BiblePublicationCategoryId == null || string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationCategoryName)))
            {
                scheduleStateItem.BiblePublicationCategoryId = publication.CategoryId;
                scheduleStateItem.BiblePublicationCategoryName = publication.Category.CategoryName;
                Log.Debug("ScheduleEffects: Set BiblePublicationCategoryId={CategoryId}, BiblePublicationCategoryName='{CategoryName}' for non-sectioned publication in schedule {ScheduleId} - initial population",
                    publication.CategoryId, publication.Category.CategoryName, scheduleId);
            }
            else if (!string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationCategoryName))
            {
                Log.Debug("ScheduleEffects: Preserving existing category '{CategoryName}' for non-sectioned publication in schedule {ScheduleId}",
                    scheduleStateItem.BiblePublicationCategoryName, scheduleId);
            }
        }
    }
}
