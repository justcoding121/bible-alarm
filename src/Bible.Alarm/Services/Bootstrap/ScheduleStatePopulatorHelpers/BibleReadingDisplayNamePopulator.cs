#nullable enable
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores.Models;
using Serilog;

namespace Bible.Alarm.Services.Bootstrap.ScheduleStatePopulatorHelpers;

/// <summary>
/// Populates Bible reading display names from cached lookup data.
/// </summary>
internal sealed class BibleReadingDisplayNamePopulator
{
    public void Populate(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        LookupDataLoader.LookupData lookupData,
        Dictionary<string, Language>? languagesDict)
    {
        if (schedule.BibleReadingSchedule == null)
        {
            return;
        }

        var bibleReading = schedule.BibleReadingSchedule;
        SetLanguageName(schedule, scheduleStateItem, bibleReading, languagesDict);
        SetPublicationName(schedule, scheduleStateItem, bibleReading, lookupData);
        SetBookName(schedule, scheduleStateItem, bibleReading, lookupData);
    }

    private static void SetLanguageName(
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

    private static void SetPublicationName(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        BibleReadingSchedule bibleReading,
        LookupDataLoader.LookupData lookupData)
    {
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
    }

    private static void SetBookName(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        BibleReadingSchedule bibleReading,
        LookupDataLoader.LookupData lookupData)
    {
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
}

