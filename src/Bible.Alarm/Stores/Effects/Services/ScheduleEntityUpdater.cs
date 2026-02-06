#nullable enable
using Bible.Alarm.Shared.Models.Schedule;
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

        // Category=Music schedules use the BiblePublicationSchedule for music content.
        // In that case, "begin with music" (AlarmMusic) must be disabled/removed.
        var isMusicCategorySchedule =
            action.Schedule != null &&
            !string.IsNullOrWhiteSpace(action.Schedule.BiblePublicationCategoryName) &&
            string.Equals(action.Schedule.BiblePublicationCategoryName, "Music", StringComparison.OrdinalIgnoreCase);

        if (isMusicCategorySchedule)
        {
            if (existing.Music != null)
            {
                Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Category=Music schedule, removing existing AlarmMusic (begin-with-music) for ScheduleId={ScheduleId}",
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
            Log.Debug("ScheduleEffects: HandleUpdateScheduleFromViewModel - action.MusicUpdated=false, skipping music update");
        }

        UpdateBiblePublicationEntity(existing, dbSchedule, action);
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
        existing.NumberOfTracksToPlay = dbSchedule.NumberOfTracksToPlay;
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
            Log.Warning("ScheduleEffects: HandleUpdateScheduleFromViewModel - dbSchedule.Music is null, skipping music update");
            return;
        }
        Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Updating music. dbSchedule.Music.TrackCode={TrackCode}, dbSchedule.Music.PublicationCode={PublicationCode}, dbSchedule.Music.LanguageCode={LanguageCode}",
            dbSchedule.Music.TrackCode, dbSchedule.Music.PublicationCode, dbSchedule.Music.LanguageCode);

        if (existing.Music == null)
        {
            Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Creating new Music entity");
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
            Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Updating existing Music. Old TrackCode={OldTrackCode}",
                oldTrackCode);

            // Always update all properties
            existing.Music.PublicationCode = dbSchedule.Music.PublicationCode;
            existing.Music.LanguageCode = dbSchedule.Music.LanguageCode;
            existing.Music.SectionCode = dbSchedule.Music.SectionCode;
            existing.Music.TrackCode = dbSchedule.Music.TrackCode;
            existing.Music.Repeat = dbSchedule.Music.Repeat;

            // Update Id if it changed
            if (dbSchedule.Music.Id > 0)
            {
                existing.Music.Id = dbSchedule.Music.Id;
            }

            Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Updated Music. New TrackCode={NewTrackCode}, PublicationCode={PublicationCode}, LanguageCode={LanguageCode}",
                existing.Music.TrackCode, existing.Music.PublicationCode, existing.Music.LanguageCode);
        }
    }

    /// <summary>
    /// Updates music entity from action schedule.
    /// </summary>
    public static void UpdateMusicFromActionSchedule(AlarmSchedule existing, UpdateScheduleFromViewModelAction action)
    {
        Log.Warning("ScheduleEffects: HandleUpdateScheduleFromViewModel - action.MusicUpdated=true but dbSchedule.Music is null. Creating Music from action.Schedule. TrackCode={TrackCode}",
            action.Schedule!.MusicTrackCode);

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
        Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Updating existing Music from action.Schedule. Old TrackCode={OldTrackCode}",
            oldTrackCode);

        existing.Music.PublicationCode = schedule.MusicPublicationCode ?? string.Empty;
        existing.Music.LanguageCode = schedule.MusicLanguageCode;
        existing.Music.SectionCode = schedule.MusicSectionCode;
        existing.Music.TrackCode = schedule.MusicTrackCode ?? string.Empty;
        existing.Music.Repeat = schedule.MusicRepeat ?? false;

        if (schedule.MusicId.HasValue)
        {
            existing.Music.Id = schedule.MusicId.Value;
        }

        Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Updated Music from action.Schedule. New TrackCode={NewTrackCode}, PublicationCode={PublicationCode}, LanguageCode={LanguageCode}",
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
        existing.LanguageCode = dbSchedule.LanguageCode;
        existing.PublicationCode = dbSchedule.PublicationCode;

        if (action.BiblePublicationUpdated)
        {
            existing.FinishedDuration = TimeSpan.Zero;
        }
    }
}

