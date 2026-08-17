#nullable enable
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Stores.Reducers.Services;

public static class SchedulePropertyCopier
{
    /// <summary>
    /// Updates the existing item in place to avoid collection change events that cause Android Auto issues.
    /// </summary>
    public static void CopyScheduleProperties(ScheduleStateItem target, ScheduleStateItem source)
    {
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

        target.BiblePublicationScheduleId = source.BiblePublicationScheduleId;
        target.BiblePublicationLanguageCode = source.BiblePublicationLanguageCode;
        target.BiblePublicationCode = source.BiblePublicationCode;
        target.BiblePublicationSectionCode = source.BiblePublicationSectionCode;
        target.BiblePublicationTrackCode = source.BiblePublicationTrackCode;
        target.BiblePublicationFinishedDuration = source.BiblePublicationFinishedDuration;

        target.MusicId = source.MusicId;
        target.MusicSectionCode = source.MusicSectionCode;
        target.MusicPublicationCode = source.MusicPublicationCode;
        target.MusicLanguageCode = source.MusicLanguageCode;
        target.MusicTrackCode = source.MusicTrackCode;
        target.MusicRepeat = source.MusicRepeat;

        target.BiblePublicationCategoryId = source.BiblePublicationCategoryId;
        target.BiblePublicationCategoryName = source.BiblePublicationCategoryName;
        target.BiblePublicationLanguageName = source.BiblePublicationLanguageName;
        target.BiblePublicationLanguageDirection = source.BiblePublicationLanguageDirection;
        target.BiblePublicationName = source.BiblePublicationName;
        target.BiblePublicationSectionName = source.BiblePublicationSectionName;
        target.BiblePublicationTrackTitle = source.BiblePublicationTrackTitle;
        target.MusicLanguageName = source.MusicLanguageName;
        target.MusicLanguageDirection = source.MusicLanguageDirection;
        target.MusicPublicationName = source.MusicPublicationName;
        target.MusicSectionName = source.MusicSectionName;
        target.MusicTrackName = source.MusicTrackName;
        target.BiblePublicationModalItemCount = source.BiblePublicationModalItemCount;
        target.BiblePublicationSectionModalItemCount = source.BiblePublicationSectionModalItemCount;
        target.BiblePublicationTrackModalItemCount = source.BiblePublicationTrackModalItemCount;
        target.MusicPublicationModalItemCount = source.MusicPublicationModalItemCount;
        target.MusicSectionModalItemCount = source.MusicSectionModalItemCount;
    }
}
