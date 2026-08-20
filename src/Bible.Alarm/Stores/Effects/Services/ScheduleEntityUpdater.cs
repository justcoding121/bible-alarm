#nullable enable
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Serilog;

namespace Bible.Alarm.Stores.Effects.Services;

public static class ScheduleEntityUpdater
{
    /// <summary>
    /// Updates an existing schedule entity with data from a new schedule entity.
    /// </summary>
    public static void UpdateScheduleEntity(AlarmSchedule existing, AlarmSchedule dbSchedule, UpdateScheduleFromViewModelAction action)
    {
        UpdateBasicScheduleProperties(existing, dbSchedule);

        var pubCode = action.Schedule?.BiblePublicationCode;
        var isMusicPublicationSchedule = (action.Schedule?.BiblePublicationIsMusic ?? false) ||
            (!string.IsNullOrWhiteSpace(pubCode) && JwSourceHelper.MusicFlagPublicationCodes.Contains(pubCode));
        if (isMusicPublicationSchedule)
        {
            if (existing.Music != null)
            {
                Log.Information(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateScheduleFromViewModelMusicPublicationRemovingAlarmMusic,
                    existing.Id);
                existing.Music = null;
            }
        }
        else if (action.MusicUpdated)
        {
            UpdateMusicEntity(existing, dbSchedule, action);
        }
        else
        {
            Log.Debug(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateScheduleFromViewModelMusicUpdatedFalseSkippingMusicUpdate);
        }

        UpdateBiblePublicationEntity(existing, dbSchedule, action);
    }

    /// <summary>
    /// Updates basic schedule properties.
    /// </summary>
    public static void UpdateBasicScheduleProperties(AlarmSchedule existing, AlarmSchedule dbSchedule)
    {
        // Preserve WeekDays if dbSchedule has it as 0 (shouldn't happen, but protect against data loss)
        if (dbSchedule.DaysOfWeek == 0 && existing.DaysOfWeek != 0)
        {
            // Don't update WeekDays - keep the existing value
        }
        else
        {
            existing.DaysOfWeek = dbSchedule.DaysOfWeek;
        }

        existing.Hour = dbSchedule.Hour;
        existing.Minute = dbSchedule.Minute;
        existing.Second = dbSchedule.Second;
        existing.IsEnabled = dbSchedule.IsEnabled;
        existing.MusicEnabled = dbSchedule.MusicEnabled;
        existing.NotificationEnabled = dbSchedule.NotificationEnabled;
        existing.AlwaysPlayFromStart = dbSchedule.AlwaysPlayFromStart;
        existing.NumberOfTracksToPlay = dbSchedule.NumberOfTracksToPlay;
        existing.Name = dbSchedule.Name;
        existing.SnoozeMinutes = dbSchedule.SnoozeMinutes;
        existing.CategoryCode = dbSchedule.CategoryCode;
        existing.LastPlayedAtUtc = dbSchedule.LastPlayedAtUtc;
    }

    /// <summary>
    /// Updates music entity if music was updated.
    /// </summary>
    public static void UpdateMusicEntity(AlarmSchedule existing, AlarmSchedule dbSchedule, UpdateScheduleFromViewModelAction action)
    {
        if (dbSchedule.Music != null)
        {
            UpdateMusicFromDbSchedule(existing, dbSchedule);
        }
        else if (HasValidMusicProperties(action.Schedule))
        {
            UpdateMusicFromActionSchedule(existing, action);
        }
        else
        {
            Log.Warning(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateScheduleFromViewModelMusicUpdatedButDbMusicNullNoValidProps);
        }
    }

    /// <summary>
    /// Checks if schedule has valid music properties.
    /// </summary>
    public static bool HasValidMusicProperties(ScheduleStateItem? schedule)
    {
        return schedule != null &&
               !string.IsNullOrEmpty(schedule.MusicPublicationCode) &&
               !string.IsNullOrWhiteSpace(schedule.MusicTrackCode);
    }

    /// <summary>
    /// Updates music entity from database schedule.
    /// </summary>
    public static void UpdateMusicFromDbSchedule(AlarmSchedule existing, AlarmSchedule dbSchedule)
    {
        if (dbSchedule.Music == null)
        {
            Log.Warning(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateScheduleFromViewModelDbMusicNullSkippingMusicUpdate);
            return;
        }
        Log.Information(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateScheduleFromViewModelUpdatingMusicDbFields,
            dbSchedule.Music.TrackCode, dbSchedule.Music.PublicationCode, dbSchedule.Music.LanguageCode);

        if (existing.Music == null)
        {
            Log.Information(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateScheduleFromViewModelCreatingNewMusicEntity);
            // Create a new tracked entity instead of using the AutoMapper-created one
            existing.Music = new AlarmMusic
            {
                Id = dbSchedule.Music.Id,
                PublicationCode = dbSchedule.Music.PublicationCode,
                LanguageCode = dbSchedule.Music.LanguageCode,
                SectionCode = dbSchedule.Music.SectionCode,
                TrackCode = dbSchedule.Music.TrackCode,
                Repeat = dbSchedule.Music.Repeat,
                AlarmScheduleId = existing.Id
            };
        }
        else
        {
            var oldTrackCode = existing.Music.TrackCode;
            Log.Information(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateScheduleFromViewModelUpdatingExistingMusicOldTrack,
                oldTrackCode);

            // Always update all properties
            existing.Music.PublicationCode = dbSchedule.Music.PublicationCode;
            existing.Music.LanguageCode = dbSchedule.Music.LanguageCode;
            existing.Music.SectionCode = dbSchedule.Music.SectionCode;
            existing.Music.TrackCode = dbSchedule.Music.TrackCode;
            existing.Music.Repeat = dbSchedule.Music.Repeat;

            if (dbSchedule.Music.Id > 0)
            {
                existing.Music.Id = dbSchedule.Music.Id;
            }

            Log.Information(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateScheduleFromViewModelUpdatedMusicNewTrackPubLang,
                existing.Music.TrackCode, existing.Music.PublicationCode, existing.Music.LanguageCode);
        }
    }

    /// <summary>
    /// Updates music entity from action schedule.
    /// </summary>
    public static void UpdateMusicFromActionSchedule(AlarmSchedule existing, UpdateScheduleFromViewModelAction action)
    {
        Log.Warning(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateScheduleFromViewModelMusicUpdatedDbMusicNullCreatingFromAction,
            action.Schedule!.MusicTrackCode);

        if (existing.Music == null)
        {
            existing.Music = CreateMusicFromActionSchedule(action.Schedule, existing.Id);
            Log.Information(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateScheduleFromViewModelCreatedNewMusicEntityFromActionSchedule);
        }
        else
        {
            UpdateExistingMusicFromActionSchedule(existing, action.Schedule);
        }
    }

    /// <summary>
    /// Creates a new music entity from action schedule.
    /// </summary>
    public static AlarmMusic CreateMusicFromActionSchedule(ScheduleStateItem schedule, int alarmScheduleId)
    {
        return new AlarmMusic
        {
            Id = schedule.MusicId ?? 0,
            PublicationCode = schedule.MusicPublicationCode ?? string.Empty,
            LanguageCode = schedule.MusicLanguageCode,
            SectionCode = schedule.MusicSectionCode,
            TrackCode = schedule.MusicTrackCode ?? string.Empty,
            Repeat = schedule.MusicRepeat ?? false,
            AlarmScheduleId = alarmScheduleId
        };
    }

    /// <summary>
    /// Updates existing music entity from action schedule.
    /// </summary>
    public static void UpdateExistingMusicFromActionSchedule(AlarmSchedule existing, ScheduleStateItem schedule)
    {
        var oldTrackCode = existing.Music!.TrackCode;
        Log.Information(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateScheduleFromViewModelUpdatingExistingMusicFromActionOldTrack,
            oldTrackCode);

        existing.Music.PublicationCode = schedule.MusicPublicationCode ?? string.Empty;
        // For no-language music (e.g. iam), we store the schedule's current language (e.g. MY), same as Bible container.
        existing.Music.LanguageCode = schedule.MusicLanguageCode;
        existing.Music.SectionCode = schedule.MusicSectionCode;
        existing.Music.TrackCode = schedule.MusicTrackCode ?? string.Empty;
        existing.Music.Repeat = schedule.MusicRepeat ?? false;

        if (schedule.MusicId.HasValue)
        {
            existing.Music.Id = schedule.MusicId.Value;
        }

        Log.Information(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateScheduleFromViewModelUpdatedMusicFromActionNewTrackPubLang,
            existing.Music.TrackCode, existing.Music.PublicationCode, existing.Music.LanguageCode);
    }

    /// <summary>
    /// Updates Bible reading entity.
    /// </summary>
    public static void UpdateBiblePublicationEntity(AlarmSchedule existing, AlarmSchedule dbSchedule, UpdateScheduleFromViewModelAction action)
    {
        if (dbSchedule.BiblePublicationSchedule == null)
        {
            return;
        }

        if (existing.BiblePublicationSchedule == null)
        {
            existing.BiblePublicationSchedule = dbSchedule.BiblePublicationSchedule;
            existing.BiblePublicationSchedule.AlarmScheduleId = existing.Id;
        }
        else
        {
            UpdateExistingBiblePublicationSchedule(existing.BiblePublicationSchedule, dbSchedule.BiblePublicationSchedule, action);
        }
    }

    /// <summary>
    /// Updates existing Bible reading schedule.
    /// </summary>
    public static void UpdateExistingBiblePublicationSchedule(
        BiblePublicationSchedule existing,
        BiblePublicationSchedule dbSchedule,
        UpdateScheduleFromViewModelAction action)
    {
        existing.SectionCode = dbSchedule.SectionCode;
        existing.TrackCode = dbSchedule.TrackCode;
        // For no-language publications, LanguageCode should be null (not "E")
        // "E" is only used in state/UI as a fallback, but should not be persisted to DB
        // This will be normalized by ScheduleSaveService, but we also normalize here for consistency
        existing.LanguageCode = dbSchedule.LanguageCode;
        existing.PublicationCode = dbSchedule.PublicationCode;

        if (action.BiblePublicationUpdated)
        {
            existing.FinishedDuration = TimeSpan.Zero;
        }
    }
}

