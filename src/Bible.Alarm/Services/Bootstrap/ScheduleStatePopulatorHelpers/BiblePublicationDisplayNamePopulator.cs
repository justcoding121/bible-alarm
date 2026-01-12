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
            if (lookupData.Publications.TryGetValue(publicationKey, out var publication) &&
                !string.IsNullOrWhiteSpace(publication.Name))
            {
                scheduleStateItem.BiblePublicationName = publication.Name;
                Log.Logger.Debug("Set BiblePublicationName '{BiblePublicationName}' for schedule {ScheduleId} (PublicationCode: {PublicationCode})",
                    publication.Name, schedule.Id, biblePublication.PublicationCode);
            }
        }
    }

    private static void SetSectionName(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        BiblePublicationSchedule biblePublication,
        LookupDataLoader.LookupData lookupData)
    {
        if (biblePublication.SectionNumber.HasValue && biblePublication.SectionNumber.Value > 0 &&
            !string.IsNullOrWhiteSpace(biblePublication.LanguageCode) &&
            !string.IsNullOrWhiteSpace(biblePublication.PublicationCode))
        {
            var sectionKey = (biblePublication.LanguageCode, biblePublication.PublicationCode, biblePublication.SectionNumber.Value);
            if (lookupData.Sections.TryGetValue(sectionKey, out var sectionName))
            {
                scheduleStateItem.BiblePublicationSectionName = sectionName;
                Log.Logger.Debug("Set BiblePublicationSectionName '{BiblePublicationSectionName}' for schedule {ScheduleId} (SectionNumber: {SectionNumber})",
                    sectionName, schedule.Id, biblePublication.SectionNumber);
            }
        }
    }

    private static void SetTrackTitle(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        BiblePublicationSchedule biblePublication,
        LookupDataLoader.LookupData lookupData)
    {
        // Only set track title for non-sectioned publications (Drama/Video)
        if (PublicationTypeHelper.HasSectionStructure(biblePublication.PublicationCode))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(biblePublication.LanguageCode) ||
            string.IsNullOrWhiteSpace(biblePublication.PublicationCode) ||
            biblePublication.TrackNumber <= 0)
        {
            return;
        }

        var publicationKey = (biblePublication.LanguageCode, biblePublication.PublicationCode);
        if (lookupData.Publications.TryGetValue(publicationKey, out var publication))
        {
            var track = publication.Tracks.FirstOrDefault(t => t.Number == biblePublication.TrackNumber);
            if (track != null && !string.IsNullOrWhiteSpace(track.Title))
            {
                scheduleStateItem.BiblePublicationTrackTitle = track.Title;
                Log.Logger.Debug("Set BiblePublicationTrackTitle '{BiblePublicationTrackTitle}' for schedule {ScheduleId} (TrackNumber: {TrackNumber})",
                    track.Title, schedule.Id, biblePublication.TrackNumber);
            }
        }
    }
}

