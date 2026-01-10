#nullable enable
using Bible;


#nullable enable
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers;

public static class ScheduleStateHelper
{
    public static ScheduleStateItem CloneScheduleStateItem(ScheduleStateItem source)
    {
        return new ScheduleStateItem
        {
            Id = source.Id,
            Name = source.Name,
            IsEnabled = source.IsEnabled,
            Hour = source.Hour,
            Minute = source.Minute,
            Second = source.Second,
            DaysOfWeek = source.DaysOfWeek,
            NotificationEnabled = source.NotificationEnabled,
            MusicEnabled = source.MusicEnabled,
            SnoozeMinutes = source.SnoozeMinutes,
            NumberOfTracksToRead = source.NumberOfTracksToRead,
            AlwaysPlayFromStart = source.AlwaysPlayFromStart,
            CurrentPlayItem = source.CurrentPlayItem,
            LatestAlarmNotificationId = source.LatestAlarmNotificationId,
            BibleReadingScheduleId = source.BibleReadingScheduleId,
            BibleReadingLanguageCode = source.BibleReadingLanguageCode,
            BibleReadingPublicationCode = source.BibleReadingPublicationCode,
            BibleReadingSectionNumber = source.BibleReadingSectionNumber,
            BibleReadingTrackNumber = source.BibleReadingTrackNumber,
            BibleReadingFinishedDuration = source.BibleReadingFinishedDuration,
            MusicId = source.MusicId,
            MusicType = source.MusicType,
            MusicPublicationCode = source.MusicPublicationCode,
            MusicLanguageCode = source.MusicLanguageCode,
            MusicTrackNumber = source.MusicTrackNumber,
            MusicRepeat = source.MusicRepeat,
            BibleReadingLanguageName = source.BibleReadingLanguageName,
            BibleReadingPublicationName = source.BibleReadingPublicationName,
            BibleReadingSectionName = source.BibleReadingSectionName,
            MusicLanguageName = source.MusicLanguageName,
            MusicPublicationName = source.MusicPublicationName,
            MusicTrackName = source.MusicTrackName
        };
    }
}

