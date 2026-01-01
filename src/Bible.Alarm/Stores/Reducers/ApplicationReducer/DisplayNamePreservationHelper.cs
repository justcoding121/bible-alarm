#nullable enable
using Bible.Alarm.Stores.Models;
using Serilog;

namespace Bible.Alarm.Stores.Reducers.ApplicationReducer;

/// <summary>
/// Handles preservation of display names when updating schedules.
/// Separated from ApplicationReducer for better modularity.
/// </summary>
public static class DisplayNamePreservationHelper
{
    public static void PreserveDisplayNamesFromExisting(ScheduleStateItem actionSchedule, ScheduleStateItem existingScheduleItem)
    {
        Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - Found existing schedule. Existing LanguageName: {ExistingLanguageName}, Action LanguageName: {ActionLanguageName}",
            existingScheduleItem.BibleReadingLanguageName ?? "null",
            actionSchedule.BibleReadingLanguageName ?? "null");

        PreserveBibleReadingDisplayNames(actionSchedule, existingScheduleItem);
        PreserveMusicDisplayNames(actionSchedule, existingScheduleItem);
    }

    public static void PreserveBibleReadingDisplayNames(ScheduleStateItem actionSchedule, ScheduleStateItem existingScheduleItem)
    {
        if (string.IsNullOrWhiteSpace(actionSchedule.BibleReadingLanguageName) && !string.IsNullOrWhiteSpace(existingScheduleItem.BibleReadingLanguageName))
        {
            Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - Preserving existing BibleReadingLanguageName: {LanguageName}",
                existingScheduleItem.BibleReadingLanguageName);
            actionSchedule.BibleReadingLanguageName = existingScheduleItem.BibleReadingLanguageName;
        }
        else
        {
            Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - Using action's BibleReadingLanguageName: {LanguageName}",
                actionSchedule.BibleReadingLanguageName ?? "null");
        }

        if (string.IsNullOrWhiteSpace(actionSchedule.BibleReadingPublicationName) && !string.IsNullOrWhiteSpace(existingScheduleItem.BibleReadingPublicationName))
        {
            actionSchedule.BibleReadingPublicationName = existingScheduleItem.BibleReadingPublicationName;
        }

        if (string.IsNullOrWhiteSpace(actionSchedule.BibleReadingBookName) && !string.IsNullOrWhiteSpace(existingScheduleItem.BibleReadingBookName))
        {
            actionSchedule.BibleReadingBookName = existingScheduleItem.BibleReadingBookName;
        }
    }

    public static void PreserveMusicDisplayNames(ScheduleStateItem actionSchedule, ScheduleStateItem existingScheduleItem)
    {
        if (string.IsNullOrWhiteSpace(actionSchedule.MusicLanguageName) && !string.IsNullOrWhiteSpace(existingScheduleItem.MusicLanguageName))
        {
            actionSchedule.MusicLanguageName = existingScheduleItem.MusicLanguageName;
        }

        if (string.IsNullOrWhiteSpace(actionSchedule.MusicPublicationName) && !string.IsNullOrWhiteSpace(existingScheduleItem.MusicPublicationName))
        {
            actionSchedule.MusicPublicationName = existingScheduleItem.MusicPublicationName;
        }

        if (string.IsNullOrWhiteSpace(actionSchedule.MusicTrackName) && !string.IsNullOrWhiteSpace(existingScheduleItem.MusicTrackName))
        {
            actionSchedule.MusicTrackName = existingScheduleItem.MusicTrackName;
        }
    }
}

