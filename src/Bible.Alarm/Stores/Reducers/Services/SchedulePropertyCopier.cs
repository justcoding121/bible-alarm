#nullable enable

#nullable enable
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Stores.Reducers.Services;

/// <summary>
/// Handles copying schedule properties between ScheduleStateItem instances.
/// Separated from ApplicationReducer for better modularity.
/// </summary>
public static class SchedulePropertyCopier
{
    /// <summary>
    /// Copies all properties from source to target ScheduleStateItem.
    /// Updates the existing item in place to avoid collection change events that cause Android Auto issues.
    /// </summary>
    public static void CopyScheduleProperties(ScheduleStateItem target, ScheduleStateItem source)
    {
        // Schedule properties
        target.Id = source.Id;
        target.Name = source.Name;
        target.IsEnabled = source.IsEnabled;
        target.Hour = source.Hour;
        target.Minute = source.Minute;
        target.Second = source.Second;
        target.DaysOfWeek = source.DaysOfWeek;
        target.NotificationEnabled = source.NotificationEnabled;
        target.MusicEnabled = source.MusicEnabled;
        target.SnoozeMinutes = source.SnoozeMinutes;
        target.NumberOfTracksToPlay = source.NumberOfTracksToPlay;
        target.AlwaysPlayFromStart = source.AlwaysPlayFromStart;
        target.CurrentPlayItem = source.CurrentPlayItem;
        target.LatestAlarmNotificationId = source.LatestAlarmNotificationId;

        // Bible Reading Schedule properties
        target.BiblePublicationScheduleId = source.BiblePublicationScheduleId;
        target.BiblePublicationLanguageCode = source.BiblePublicationLanguageCode;
        target.BiblePublicationCode = source.BiblePublicationCode;
        target.BiblePublicationSectionNumber = source.BiblePublicationSectionNumber;
        target.BiblePublicationTrackNumber = source.BiblePublicationTrackNumber;
        target.BiblePublicationFinishedDuration = source.BiblePublicationFinishedDuration;

        // Music properties
        target.MusicId = source.MusicId;
        target.MusicSectionCode = source.MusicSectionCode;
        target.MusicType = source.MusicType;
        target.MusicPublicationCode = source.MusicPublicationCode;
        target.MusicLanguageCode = source.MusicLanguageCode;
        target.MusicTrackNumber = source.MusicTrackNumber;
        target.MusicRepeat = source.MusicRepeat;

        // Display name properties
        target.BiblePublicationLanguageName = source.BiblePublicationLanguageName;
        target.BiblePublicationLanguageDirection = source.BiblePublicationLanguageDirection;
        target.BiblePublicationName = source.BiblePublicationName;
        target.BiblePublicationSectionName = source.BiblePublicationSectionName;
        target.MusicLanguageName = source.MusicLanguageName;
        target.MusicLanguageDirection = source.MusicLanguageDirection;
        target.MusicPublicationName = source.MusicPublicationName;
        target.MusicTrackName = source.MusicTrackName;
    }
}

