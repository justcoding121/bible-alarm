#nullable enable
using Bible;
using Bible.Alarm.Stores.Models;
using Serilog;

namespace Bible.Alarm.Stores.Reducers.Services;

/// <summary>
/// Handles preservation of display names when updating schedules.
/// Separated from ApplicationReducer for better modularity.
/// </summary>
public static class DisplayNamePreservationHelper
{
    public static void PreserveDisplayNamesFromExisting(ScheduleStateItem actionSchedule, ScheduleStateItem existingScheduleItem)
    {
        Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - Found existing schedule. Existing LanguageName: {ExistingLanguageName}, Action LanguageName: {ActionLanguageName}",
            existingScheduleItem.BiblePublicationLanguageName ?? "null",
            actionSchedule.BiblePublicationLanguageName ?? "null");

        PreserveBiblePublicationDisplayNames(actionSchedule, existingScheduleItem);
        PreserveMusicDisplayNames(actionSchedule, existingScheduleItem);
    }

    public static void PreserveBiblePublicationDisplayNames(ScheduleStateItem actionSchedule, ScheduleStateItem existingScheduleItem)
    {
        if (string.IsNullOrWhiteSpace(actionSchedule.BiblePublicationLanguageName) && !string.IsNullOrWhiteSpace(existingScheduleItem.BiblePublicationLanguageName))
        {
            Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - Preserving existing BiblePublicationLanguageName: {LanguageName}",
                existingScheduleItem.BiblePublicationLanguageName);
            actionSchedule.BiblePublicationLanguageName = existingScheduleItem.BiblePublicationLanguageName;
        }
        else
        {
            Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - Using action's BiblePublicationLanguageName: {LanguageName}",
                actionSchedule.BiblePublicationLanguageName ?? "null");
        }

        if (string.IsNullOrWhiteSpace(actionSchedule.BiblePublicationName) && !string.IsNullOrWhiteSpace(existingScheduleItem.BiblePublicationName))
        {
            actionSchedule.BiblePublicationName = existingScheduleItem.BiblePublicationName;
        }

        if (string.IsNullOrWhiteSpace(actionSchedule.BiblePublicationSectionName) && !string.IsNullOrWhiteSpace(existingScheduleItem.BiblePublicationSectionName))
        {
            actionSchedule.BiblePublicationSectionName = existingScheduleItem.BiblePublicationSectionName;
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

