#nullable enable
using Bible.Alarm.Shared.Constants;
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
        SetLanguageName(schedule, scheduleStateItem, biblePublication, languagesDict, lookupData);
        SetPublicationName(schedule, scheduleStateItem, biblePublication, lookupData);
        SetSectionName(schedule, scheduleStateItem, biblePublication, lookupData);
        SetTrackTitle(schedule, scheduleStateItem, biblePublication, lookupData);
    }

    private static void SetLanguageName(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        BiblePublicationSchedule biblePublication,
        Dictionary<string, Language>? languagesDict,
        LookupDataLoader.LookupData lookupData)
    {
        if (languagesDict == null)
        {
            return;
        }

        // No-language publications (e.g. "iam") store LanguageCode "E" as the effective code for playback.
        // Set the display name for that code (e.g. "English") so the UI shows "English" instead of "E".
        if (!string.IsNullOrWhiteSpace(biblePublication.PublicationCode) &&
            lookupData.NoLanguagePublications.ContainsKey(biblePublication.PublicationCode))
        {
            var effectiveCode = biblePublication.LanguageCode;
            if (!string.IsNullOrWhiteSpace(effectiveCode) && languagesDict.TryGetValue(effectiveCode, out var effectiveLanguage))
            {
                scheduleStateItem.BiblePublicationLanguageName = effectiveLanguage.Name;
                scheduleStateItem.BiblePublicationLanguageDirection = effectiveLanguage.Direction;
                Log.Logger.Debug("Set BiblePublicationLanguageName '{BiblePublicationLanguageName}' for no-language publication {PublicationCode} in schedule {ScheduleId} (effective LanguageCode: {LanguageCode})",
                    effectiveLanguage.Name, biblePublication.PublicationCode, schedule.Id, effectiveCode);
            }
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
            // Default to LTR
            scheduleStateItem.BiblePublicationLanguageDirection = "ltr";
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
        if (string.IsNullOrWhiteSpace(biblePublication.PublicationCode))
        {
            return;
        }

        var isNonLanguagePublication = string.IsNullOrWhiteSpace(biblePublication.LanguageCode);

        // For non-language publications (LanguageCode is null in DB, LanguageId == null in media index),
        // check no-language lookups first since they're specifically designed for LanguageId == null publications.
        if (isNonLanguagePublication)
        {
            if (lookupData.NoLanguagePublications.TryGetValue(biblePublication.PublicationCode, out var noLangMeta))
            {
                if (!string.IsNullOrWhiteSpace(noLangMeta.Name))
                {
                    scheduleStateItem.BiblePublicationName = noLangMeta.Name;
                    Log.Logger.Debug("Set BiblePublicationName '{BiblePublicationName}' (no-language) for schedule {ScheduleId} (PublicationCode: {PublicationCode})",
                        noLangMeta.Name, schedule.Id, biblePublication.PublicationCode);
                }

                if (!string.IsNullOrWhiteSpace(noLangMeta.CategoryName))
                {
                    scheduleStateItem.BiblePublicationCategoryId = noLangMeta.CategoryId;
                    scheduleStateItem.BiblePublicationCategoryName = noLangMeta.CategoryName;
                    Log.Logger.Debug("Set BiblePublicationCategoryId={CategoryId}, BiblePublicationCategoryName='{CategoryName}' (no-language) for schedule {ScheduleId} (PublicationCode: {PublicationCode})",
                        noLangMeta.CategoryId, noLangMeta.CategoryName, schedule.Id, biblePublication.PublicationCode);
                }
                return;
            }
        }

        // For language-bound publications, or fallback for non-language if no-language lookup didn't find it.
        // Use default language code for non-language publications to match LookupKeys collection.
        var effectiveLanguageCode = isNonLanguagePublication ? AppConstants.Media.DefaultLanguageCode : biblePublication.LanguageCode;
        var publicationKey = (effectiveLanguageCode, biblePublication.PublicationCode);
        if (lookupData.Publications.TryGetValue(publicationKey, out var publication))
        {
            if (!string.IsNullOrWhiteSpace(publication.Name))
            {
                scheduleStateItem.BiblePublicationName = publication.Name;
                Log.Logger.Debug("Set BiblePublicationName '{BiblePublicationName}' for schedule {ScheduleId} (PublicationCode: {PublicationCode}, effective LanguageCode: {EffectiveLanguageCode})",
                    publication.Name, schedule.Id, biblePublication.PublicationCode, effectiveLanguageCode);
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

    private static void SetSectionName(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        BiblePublicationSchedule biblePublication,
        LookupDataLoader.LookupData lookupData)
    {
        var sectionCode = SectionCodeHelper.Normalize(biblePublication.SectionCode);
        if (string.IsNullOrWhiteSpace(sectionCode) || string.IsNullOrWhiteSpace(biblePublication.PublicationCode))
        {
            return;
        }

        var isNonLanguagePublication = string.IsNullOrWhiteSpace(biblePublication.LanguageCode);

        // For non-language publications (LanguageCode is null in DB, LanguageId == null in media index),
        // check no-language lookups first since they're specifically designed for LanguageId == null publications.
        if (isNonLanguagePublication)
        {
            if (lookupData.NoLanguageSections.TryGetValue((biblePublication.PublicationCode, sectionCode), out var noLangSectionName))
            {
                scheduleStateItem.BiblePublicationSectionName = noLangSectionName;
                Log.Logger.Debug("Set BiblePublicationSectionName '{BiblePublicationSectionName}' (no-language) for schedule {ScheduleId} (SectionCode: {SectionCode})",
                    noLangSectionName, schedule.Id, biblePublication.SectionCode);
                return;
            }
        }

        // For language-bound publications, or fallback for non-language if no-language lookup didn't find it.
        // Use default language code for non-language publications to match LookupKeys collection.
        var effectiveLanguageCode = isNonLanguagePublication ? AppConstants.Media.DefaultLanguageCode : biblePublication.LanguageCode;
        var sectionKey = (effectiveLanguageCode, biblePublication.PublicationCode, sectionCode);
        if (lookupData.Sections.TryGetValue(sectionKey, out var sectionName))
        {
            scheduleStateItem.BiblePublicationSectionName = sectionName;
            Log.Logger.Debug("Set BiblePublicationSectionName '{BiblePublicationSectionName}' for schedule {ScheduleId} (SectionCode: {SectionCode}, effective LanguageCode: {EffectiveLanguageCode})",
                sectionName, schedule.Id, biblePublication.SectionCode, effectiveLanguageCode);
        }
    }

    private static void SetTrackTitle(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        BiblePublicationSchedule biblePublication,
        LookupDataLoader.LookupData lookupData)
    {
        if (string.IsNullOrWhiteSpace(biblePublication.PublicationCode) ||
            string.IsNullOrWhiteSpace(biblePublication.TrackCode))
        {
            return;
        }

        var isNonLanguagePublication = string.IsNullOrWhiteSpace(biblePublication.LanguageCode);

        // For non-language publications (LanguageCode is null in DB, LanguageId == null in media index),
        // check no-language lookups first since they're specifically designed for LanguageId == null publications.
        if (isNonLanguagePublication)
        {
            var normalizedSectionCode = SectionCodeHelper.Normalize(biblePublication.SectionCode);
            if (lookupData.NoLanguageTrackTitles.TryGetValue((biblePublication.PublicationCode, normalizedSectionCode, biblePublication.TrackCode), out var noLangTitle))
            {
                scheduleStateItem.BiblePublicationTrackTitle = noLangTitle;
                Log.Logger.Debug("Set BiblePublicationTrackTitle '{BiblePublicationTrackTitle}' (no-language) for schedule {ScheduleId} (SectionCode: {SectionCode}, TrackCode: {TrackCode})",
                    noLangTitle, schedule.Id, biblePublication.SectionCode, biblePublication.TrackCode);
                return;
            }
        }

        // For language-bound publications, or fallback for non-language if no-language lookup didn't find it.
        // Use default language code for non-language publications to match LookupKeys collection.
        var effectiveLanguageCode = isNonLanguagePublication ? AppConstants.Media.DefaultLanguageCode : biblePublication.LanguageCode;
        var publicationKey = (effectiveLanguageCode, biblePublication.PublicationCode);
        if (lookupData.Publications.TryGetValue(publicationKey, out var publication))
        {
            // For sectioned publications, find track in the section's tracks
            if (PublicationTypeHelper.HasSectionStructure(biblePublication.PublicationCode))
            {
                var sectionCode = SectionCodeHelper.Normalize(biblePublication.SectionCode);
                if (string.IsNullOrWhiteSpace(sectionCode))
                {
                    return;
                }

                // Find the section in the publication
                var section = publication.Sections?.FirstOrDefault(s =>
                    !string.IsNullOrWhiteSpace(s.SectionCode) &&
                    string.Equals(s.SectionCode, sectionCode, StringComparison.OrdinalIgnoreCase));
        
                if (section != null)
                {
                    // Find the track in the section's tracks
                    var track = section.Tracks?.FirstOrDefault(t => !string.IsNullOrWhiteSpace(biblePublication.TrackCode) &&
                        t.TrackCode == biblePublication.TrackCode);
                    if (track != null && !string.IsNullOrWhiteSpace(track.Title))
                    {
                        scheduleStateItem.BiblePublicationTrackTitle = track.Title;
                        Log.Logger.Debug("Set BiblePublicationTrackTitle '{BiblePublicationTrackTitle}' for schedule {ScheduleId} (SectionCode: {SectionCode}, TrackCode: {TrackCode}, effective LanguageCode: {EffectiveLanguageCode})",
                            track.Title, schedule.Id, biblePublication.SectionCode, biblePublication.TrackCode, effectiveLanguageCode);
                    }
                }
            }
            else
            {
                // For non-sectioned publications (Drama/Video), find track directly in publication's tracks
                var track = publication.Tracks?.FirstOrDefault(t => !string.IsNullOrWhiteSpace(biblePublication.TrackCode) &&
                    t.TrackCode == biblePublication.TrackCode);
                if (track != null && !string.IsNullOrWhiteSpace(track.Title))
                {
                    scheduleStateItem.BiblePublicationTrackTitle = track.Title;
                    Log.Logger.Debug("Set BiblePublicationTrackTitle '{BiblePublicationTrackTitle}' for schedule {ScheduleId} (TrackCode: {TrackCode}, effective LanguageCode: {EffectiveLanguageCode})",
                        track.Title, schedule.Id, biblePublication.TrackCode, effectiveLanguageCode);
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
}

