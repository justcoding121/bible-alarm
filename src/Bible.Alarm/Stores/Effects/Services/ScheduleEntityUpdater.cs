#nullable enable
using Bible;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Serilog;

namespace Bible.Alarm.Stores.Effects.Services;

/// <summary>
/// Handles updating of schedule entities in the database.
/// Separated from ScheduleEffects for better modularity.
/// </summary>
public static class ScheduleEntityUpdater
{
    /// <summary>
    /// Updates an existing schedule entity with data from a new schedule entity.
    /// </summary>
    public static void UpdateScheduleEntity(AlarmSchedule existing, AlarmSchedule dbSchedule, UpdateScheduleFromViewModelAction action)
    {
        UpdateBasicScheduleProperties(existing, dbSchedule);

        if (action.MusicUpdated)
        {
            UpdateMusicEntity(existing, dbSchedule, action);
        }
        else
        {
            Log.Debug("ScheduleEffects: HandleUpdateScheduleFromViewModel - action.MusicUpdated=false, skipping music update");
        }

        UpdateBibleReadingEntity(existing, dbSchedule, action);
    }

    /// <summary>
    /// Updates basic schedule properties.
    /// </summary>
    public static void UpdateBasicScheduleProperties(AlarmSchedule existing, AlarmSchedule dbSchedule)
    {
        existing.Hour = dbSchedule.Hour;
        existing.Minute = dbSchedule.Minute;
        existing.Second = dbSchedule.Second;
        existing.DaysOfWeek = dbSchedule.DaysOfWeek;
        existing.IsEnabled = dbSchedule.IsEnabled;
        existing.MusicEnabled = dbSchedule.MusicEnabled;
        existing.NotificationEnabled = dbSchedule.NotificationEnabled;
        existing.AlwaysPlayFromStart = dbSchedule.AlwaysPlayFromStart;
        existing.NumberOfChaptersToRead = dbSchedule.NumberOfChaptersToRead;
        existing.Name = dbSchedule.Name;
        existing.SnoozeMinutes = dbSchedule.SnoozeMinutes;
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
            Log.Warning("ScheduleEffects: HandleUpdateScheduleFromViewModel - action.MusicUpdated=true but dbSchedule.Music is null and action.Schedule has no valid music properties");
        }
    }

    /// <summary>
    /// Checks if schedule has valid music properties.
    /// </summary>
    public static bool HasValidMusicProperties(ScheduleStateItem? schedule)
    {
        return schedule != null &&
               schedule.MusicType.HasValue &&
               schedule.MusicTrackNumber.HasValue &&
               schedule.MusicTrackNumber.Value > 0;
    }

    /// <summary>
    /// Updates music entity from database schedule.
    /// </summary>
    public static void UpdateMusicFromDbSchedule(AlarmSchedule existing, AlarmSchedule dbSchedule)
    {
        if (dbSchedule.Music == null)
        {
            Log.Warning("ScheduleEffects: HandleUpdateScheduleFromViewModel - dbSchedule.Music is null, skipping music update");
            return;
        }
        Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Updating music. dbSchedule.Music.MusicType={MusicType}, dbSchedule.Music.TrackNumber={TrackNumber}, dbSchedule.Music.PublicationCode={PublicationCode}, dbSchedule.Music.LanguageCode={LanguageCode}",
            dbSchedule.Music.MusicType, dbSchedule.Music.TrackNumber, dbSchedule.Music.PublicationCode, dbSchedule.Music.LanguageCode);

        if (existing.Music == null)
        {
            Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Creating new Music entity");
            // Create a new tracked entity instead of using the AutoMapper-created one
            existing.Music = new AlarmMusic
            {
                Id = dbSchedule.Music.Id,
                MusicType = dbSchedule.Music.MusicType,
                PublicationCode = dbSchedule.Music.PublicationCode,
                LanguageCode = dbSchedule.Music.LanguageCode,
                TrackNumber = dbSchedule.Music.TrackNumber,
                Repeat = dbSchedule.Music.Repeat,
                AlarmScheduleId = existing.Id
            };
        }
        else
        {
            var oldMusicType = existing.Music.MusicType;
            var oldTrackNumber = existing.Music.TrackNumber;
            Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Updating existing Music. Old MusicType={OldMusicType}, Old TrackNumber={OldTrackNumber}",
                oldMusicType, oldTrackNumber);

            // Always update all properties to ensure MusicType changes are saved
            existing.Music.MusicType = dbSchedule.Music.MusicType;
            existing.Music.PublicationCode = dbSchedule.Music.PublicationCode;
            existing.Music.LanguageCode = dbSchedule.Music.LanguageCode;
            existing.Music.TrackNumber = dbSchedule.Music.TrackNumber;
            existing.Music.Repeat = dbSchedule.Music.Repeat;
            
            // Update Id if it changed (e.g., when music type changes, MusicId might be reset)
            if (dbSchedule.Music.Id > 0)
            {
                existing.Music.Id = dbSchedule.Music.Id;
            }

            Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Updated Music. New MusicType={NewMusicType}, New TrackNumber={NewTrackNumber}, PublicationCode={PublicationCode}, LanguageCode={LanguageCode}",
                existing.Music.MusicType, existing.Music.TrackNumber, existing.Music.PublicationCode, existing.Music.LanguageCode);
        }
    }

    /// <summary>
    /// Updates music entity from action schedule.
    /// </summary>
    public static void UpdateMusicFromActionSchedule(AlarmSchedule existing, UpdateScheduleFromViewModelAction action)
    {
        Log.Warning("ScheduleEffects: HandleUpdateScheduleFromViewModel - action.MusicUpdated=true but dbSchedule.Music is null. Creating Music from action.Schedule. MusicType={MusicType}, TrackNumber={TrackNumber}",
            action.Schedule!.MusicType, action.Schedule.MusicTrackNumber);

        if (existing.Music == null)
        {
            existing.Music = CreateMusicFromActionSchedule(action.Schedule, existing.Id);
            Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Created new Music entity from action.Schedule");
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
            MusicType = schedule.MusicType!.Value,
            PublicationCode = schedule.MusicPublicationCode ?? string.Empty,
            LanguageCode = schedule.MusicLanguageCode,
            TrackNumber = schedule.MusicTrackNumber!.Value,
            Repeat = schedule.MusicRepeat ?? false,
            AlarmScheduleId = alarmScheduleId
        };
    }

    /// <summary>
    /// Updates existing music entity from action schedule.
    /// </summary>
    public static void UpdateExistingMusicFromActionSchedule(AlarmSchedule existing, ScheduleStateItem schedule)
    {
        var oldMusicType = existing.Music!.MusicType;
        var oldTrackNumber = existing.Music.TrackNumber;
        Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Updating existing Music from action.Schedule. Old MusicType={OldMusicType}, Old TrackNumber={OldTrackNumber}",
            oldMusicType, oldTrackNumber);

        existing.Music.MusicType = schedule.MusicType!.Value;
        existing.Music.PublicationCode = schedule.MusicPublicationCode ?? string.Empty;
        existing.Music.LanguageCode = schedule.MusicLanguageCode;
        existing.Music.TrackNumber = schedule.MusicTrackNumber!.Value;
        existing.Music.Repeat = schedule.MusicRepeat ?? false;

        if (schedule.MusicId.HasValue)
        {
            existing.Music.Id = schedule.MusicId.Value;
        }

        Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Updated Music from action.Schedule. New MusicType={NewMusicType}, New TrackNumber={NewTrackNumber}, PublicationCode={PublicationCode}, LanguageCode={LanguageCode}",
            existing.Music.MusicType, existing.Music.TrackNumber, existing.Music.PublicationCode, existing.Music.LanguageCode);
    }

    /// <summary>
    /// Updates Bible reading entity.
    /// </summary>
    public static void UpdateBibleReadingEntity(AlarmSchedule existing, AlarmSchedule dbSchedule, UpdateScheduleFromViewModelAction action)
    {
        if (dbSchedule.BibleReadingSchedule == null)
        {
            return;
        }

        if (existing.BibleReadingSchedule == null)
        {
            existing.BibleReadingSchedule = dbSchedule.BibleReadingSchedule;
            existing.BibleReadingSchedule.AlarmScheduleId = existing.Id;
        }
        else
        {
            UpdateExistingBibleReadingSchedule(existing.BibleReadingSchedule, dbSchedule.BibleReadingSchedule, action);
        }
    }

    /// <summary>
    /// Updates existing Bible reading schedule.
    /// </summary>
    public static void UpdateExistingBibleReadingSchedule(
        BibleReadingSchedule existing,
        BibleReadingSchedule dbSchedule,
        UpdateScheduleFromViewModelAction action)
    {
        existing.SectionNumber = dbSchedule.SectionNumber;
        existing.ChapterNumber = dbSchedule.ChapterNumber;
        existing.LanguageCode = dbSchedule.LanguageCode;
        existing.PublicationCode = dbSchedule.PublicationCode;

        if (action.BibleReadingUpdated)
        {
            existing.FinishedDuration = TimeSpan.Zero;
        }
    }
}

