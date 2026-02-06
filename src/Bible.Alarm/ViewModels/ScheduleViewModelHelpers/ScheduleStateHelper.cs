#nullable enable


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
            NumberOfTracksToPlay = source.NumberOfTracksToPlay,
            AlwaysPlayFromStart = source.AlwaysPlayFromStart,
            CurrentPlayItem = source.CurrentPlayItem,
            LatestAlarmNotificationId = source.LatestAlarmNotificationId,
            BiblePublicationScheduleId = source.BiblePublicationScheduleId,
            BiblePublicationLanguageCode = source.BiblePublicationLanguageCode,
            BiblePublicationCode = source.BiblePublicationCode,
            BiblePublicationSectionCode = source.BiblePublicationSectionCode,
            BiblePublicationTrackCode = source.BiblePublicationTrackCode,
            BiblePublicationFinishedDuration = source.BiblePublicationFinishedDuration,
            MusicId = source.MusicId,
            MusicSectionCode = source.MusicSectionCode,
            MusicPublicationCode = source.MusicPublicationCode,
            MusicLanguageCode = source.MusicLanguageCode,
            MusicTrackCode = source.MusicTrackCode,
            MusicRepeat = source.MusicRepeat,
            BiblePublicationLanguageName = source.BiblePublicationLanguageName,
            BiblePublicationName = source.BiblePublicationName,
            BiblePublicationSectionName = source.BiblePublicationSectionName,
            MusicLanguageName = source.MusicLanguageName,
            MusicPublicationName = source.MusicPublicationName,
            MusicTrackName = source.MusicTrackName
        };
    }
}

