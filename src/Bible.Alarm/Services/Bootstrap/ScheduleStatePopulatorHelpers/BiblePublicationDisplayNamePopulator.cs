#nullable enable
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores.Models;
using Serilog;

namespace Bible.Alarm.Services.Bootstrap.ScheduleStatePopulatorHelpers;

/// <summary>
/// Populates Bible reading display names from cached lookup data.
/// </summary>
internal sealed class BiblePublicationDisplayNamePopulator
{
    public void Populate(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        LookupDataLoader.LookupData lookupData,
        Dictionary<string, Language>? languagesDict)
    {
        if (schedule.BiblePublicationSchedule == null)
        {
            return;
        }

        var biblePublication = schedule.BiblePublicationSchedule;
        SetLanguageName(schedule, scheduleStateItem, biblePublication, languagesDict);
        SetPublicationName(schedule, scheduleStateItem, biblePublication, lookupData);
        SetSectionName(schedule, scheduleStateItem, biblePublication, lookupData);
        SetTrackTitle(schedule, scheduleStateItem, biblePublication, lookupData);
    }

    private static void SetLanguageName(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        BiblePublicationSchedule biblePublication,
        Dictionary<string, Language>? languagesDict)
    {
        if (languagesDict == null)
        {
            return;
        }

        var languageCode = biblePublication.LanguageCode;
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return;
        }

        if (languagesDict.TryGetValue(languageCode, out var language))
        {
            scheduleStateItem.BiblePublicationLanguageName = language.Name;
            scheduleStateItem.BiblePublicationLanguageDirection = language.Direction;
            Log.Logger.Debug("Set BiblePublicationLanguageName '{BiblePublicationLanguageName}' and Direction '{Direction}' for schedule {ScheduleId} (LanguageCode: {LanguageCode})",
                language.Name, language.Direction, schedule.Id, languageCode);
        }
        else
        {
            scheduleStateItem.BiblePublicationLanguageName = languageCode;
            scheduleStateItem.BiblePublicationLanguageDirection = "ltr"; // Default to LTR
            Log.Logger.Debug("Language not found for LanguageCode '{LanguageCode}', using code as BiblePublicationLanguageName for schedule {ScheduleId}",
                languageCode, schedule.Id);
        }
    }

    private static void SetPublicationName(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        BiblePublicationSchedule biblePublication,
        LookupDataLoader.LookupData lookupData)
    {
        if (!string.IsNullOrWhiteSpace(biblePublication.LanguageCode) &&
            !string.IsNullOrWhiteSpace(biblePublication.PublicationCode))
        {
            var publicationKey = (biblePublication.LanguageCode, biblePublication.PublicationCode);
            if (lookupData.Publications.TryGetValue(publicationKey, out var publication))
            {
                if (!string.IsNullOrWhiteSpace(publication.Name))
                {
                    scheduleStateItem.BiblePublicationName = publication.Name;
                    Log.Logger.Debug("Set BiblePublicationName '{BiblePublicationName}' for schedule {ScheduleId} (PublicationCode: {PublicationCode})",
                        publication.Name, schedule.Id, biblePublication.PublicationCode);
                }

                // Populate category from publication
                if (publication.Category != null)
                {
                    scheduleStateItem.BiblePublicationCategoryId = publication.CategoryId;
                    scheduleStateItem.BiblePublicationCategoryName = publication.Category.CategoryName;
                    Log.Logger.Debug("Set BiblePublicationCategoryId={CategoryId}, BiblePublicationCategoryName='{CategoryName}' for schedule {ScheduleId} (PublicationCode: {PublicationCode})",
                        publication.CategoryId, publication.Category.CategoryName, schedule.Id, biblePublication.PublicationCode);
                }
                else
                {
                    // Fallback: derive category name from publication code
                    var categoryName = JwSourceHelper.GetCategoryName(biblePublication.PublicationCode);
                    if (!string.IsNullOrWhiteSpace(categoryName))
                    {
                        scheduleStateItem.BiblePublicationCategoryName = categoryName;
                        Log.Logger.Debug("Set BiblePublicationCategoryName='{CategoryName}' from publication code for schedule {ScheduleId} (PublicationCode: {PublicationCode})",
                            categoryName, schedule.Id, biblePublication.PublicationCode);
                    }
                }
            }
        }
    }

    private static void SetSectionName(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        BiblePublicationSchedule biblePublication,
        LookupDataLoader.LookupData lookupData)
    {
        // Convert SectionCode to int for lookup key
        if (!string.IsNullOrEmpty(biblePublication.SectionCode) && 
            int.TryParse(biblePublication.SectionCode, out var sectionCode) && 
            sectionCode > 0 &&
            !string.IsNullOrWhiteSpace(biblePublication.LanguageCode) &&
            !string.IsNullOrWhiteSpace(biblePublication.PublicationCode))
        {
            var sectionKey = (biblePublication.LanguageCode, biblePublication.PublicationCode, sectionCode);
            if (lookupData.Sections.TryGetValue(sectionKey, out var sectionName))
            {
                scheduleStateItem.BiblePublicationSectionName = sectionName;
                Log.Logger.Debug("Set BiblePublicationSectionName '{BiblePublicationSectionName}' for schedule {ScheduleId} (SectionCode: {SectionCode})",
                    sectionName, schedule.Id, biblePublication.SectionCode);
            }
        }
    }

    private static void SetTrackTitle(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        BiblePublicationSchedule biblePublication,
        LookupDataLoader.LookupData lookupData)
    {
        if (string.IsNullOrWhiteSpace(biblePublication.LanguageCode) ||
            string.IsNullOrWhiteSpace(biblePublication.PublicationCode) ||
            biblePublication.TrackNumber <= 0)
        {
            return;
        }

        var publicationKey = (biblePublication.LanguageCode, biblePublication.PublicationCode);
        if (!lookupData.Publications.TryGetValue(publicationKey, out var publication))
        {
            return;
        }

        // For sectioned publications, find track in the section's tracks
        if (PublicationTypeHelper.HasSectionStructure(biblePublication.PublicationCode))
        {
            // Convert SectionCode to int for lookup
            if (string.IsNullOrEmpty(biblePublication.SectionCode) || 
                !int.TryParse(biblePublication.SectionCode, out var sectionCode) || 
                sectionCode <= 0)
            {
                return;
            }

            // Find the section in the publication
            var section = publication.Sections?.FirstOrDefault(s =>
                s.TryGetSectionIndex().HasValue &&
                s.TryGetSectionIndex()!.Value == sectionCode);
            
            if (section != null)
            {
                // Find the track in the section's tracks
                var track = section.Tracks?.FirstOrDefault(t => t.Number == biblePublication.TrackNumber);
                if (track != null && !string.IsNullOrWhiteSpace(track.Title))
                {
                    scheduleStateItem.BiblePublicationTrackTitle = track.Title;
                    Log.Logger.Debug("Set BiblePublicationTrackTitle '{BiblePublicationTrackTitle}' for schedule {ScheduleId} (SectionCode: {SectionCode}, TrackNumber: {TrackNumber})",
                        track.Title, schedule.Id, biblePublication.SectionCode, biblePublication.TrackNumber);
                }
            }
        }
        else
        {
            // For non-sectioned publications (Drama/Video), find track directly in publication's tracks
            var track = publication.Tracks?.FirstOrDefault(t => t.Number == biblePublication.TrackNumber);
            if (track != null && !string.IsNullOrWhiteSpace(track.Title))
            {
                scheduleStateItem.BiblePublicationTrackTitle = track.Title;
                Log.Logger.Debug("Set BiblePublicationTrackTitle '{BiblePublicationTrackTitle}' for schedule {ScheduleId} (TrackNumber: {TrackNumber})",
                    track.Title, schedule.Id, biblePublication.TrackNumber);
            }

            // Populate category from publication (for non-sectioned publications that weren't loaded earlier)
            if (publication.Category != null && 
                (scheduleStateItem.BiblePublicationCategoryId == null || string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationCategoryName)))
            {
                scheduleStateItem.BiblePublicationCategoryId = publication.CategoryId;
                scheduleStateItem.BiblePublicationCategoryName = publication.Category.CategoryName;
                Log.Logger.Debug("Set BiblePublicationCategoryId={CategoryId}, BiblePublicationCategoryName='{CategoryName}' for non-sectioned publication in schedule {ScheduleId}",
                    publication.CategoryId, publication.Category.CategoryName, schedule.Id);
            }
        }
    }
}

