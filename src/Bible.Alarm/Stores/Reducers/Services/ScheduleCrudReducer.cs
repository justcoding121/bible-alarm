#nullable enable
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Serilog;

namespace Bible.Alarm.Stores.Reducers.Services;

public static class ScheduleCrudReducer
{
    public static ApplicationState OnCreateSchedule(ApplicationState state, CreateScheduleAction action)
    {
        Log.Information(AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnCreateScheduleNameExistingCount,
            action.Schedule?.Name, state.Schedules?.Count ?? 0);

        if (action.Schedule == null)
        {
            return state;
        }

        var newSchedules = new ObservableHashSet<ScheduleStateItem>();
        if (state.Schedules != null)
        {
            foreach (var scheduleItem in state.Schedules)
            {
                newSchedules.Add(scheduleItem);
            }
        }

        // Add optimistically with a temporary ID (will be replaced on success)
        var optimisticSchedule = CreateOptimisticSchedule(action.Schedule);
        newSchedules.Add(optimisticSchedule);

        Log.Information(AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnCreateScheduleNewSchedulesCount, newSchedules.Count);

        return StateFactory.CreateStateWithSchedules(state, newSchedules, optimisticSchedule);
    }

    private static ScheduleStateItem CreateOptimisticSchedule(ScheduleStateItem actionSchedule)
    {
        return new ScheduleStateItem
        {
            Id = actionSchedule.Id == 0 ? -1 : actionSchedule.Id,
            Name = actionSchedule.Name,
            IsEnabled = actionSchedule.IsEnabled,
            Hour = actionSchedule.Hour,
            Minute = actionSchedule.Minute,
            Second = actionSchedule.Second,
            DaysOfWeek = actionSchedule.DaysOfWeek,
            NotificationEnabled = actionSchedule.NotificationEnabled,
            MusicEnabled = actionSchedule.MusicEnabled,
            SnoozeMinutes = actionSchedule.SnoozeMinutes,
            NumberOfTracksToPlay = actionSchedule.NumberOfTracksToPlay,
            AlwaysPlayFromStart = actionSchedule.AlwaysPlayFromStart,
            CurrentPlayItem = actionSchedule.CurrentPlayItem,
            LatestAlarmNotificationId = actionSchedule.LatestAlarmNotificationId,
            BiblePublicationScheduleId = actionSchedule.BiblePublicationScheduleId,
            BiblePublicationLanguageCode = actionSchedule.BiblePublicationLanguageCode,
            BiblePublicationCode = actionSchedule.BiblePublicationCode,
            BiblePublicationSectionCode = actionSchedule.BiblePublicationSectionCode,
            BiblePublicationTrackCode = actionSchedule.BiblePublicationTrackCode,
            BiblePublicationFinishedDuration = actionSchedule.BiblePublicationFinishedDuration,
            MusicId = actionSchedule.MusicId,
            MusicSectionCode = actionSchedule.MusicSectionCode,
            MusicPublicationCode = actionSchedule.MusicPublicationCode,
            MusicLanguageCode = actionSchedule.MusicLanguageCode,
            MusicTrackCode = actionSchedule.MusicTrackCode,
            MusicRepeat = actionSchedule.MusicRepeat,
            BiblePublicationLanguageName = actionSchedule.BiblePublicationLanguageName,
            BiblePublicationName = actionSchedule.BiblePublicationName,
            MusicLanguageName = actionSchedule.MusicLanguageName,
            MusicPublicationName = actionSchedule.MusicPublicationName,
            MusicTrackName = actionSchedule.MusicTrackName
        };
    }

    public static ApplicationState OnDeleteSchedule(ApplicationState state, DeleteScheduleAction action)
    {
        Log.Information(AppConstants.Logging.ScheduleCrudReducerDiagnosticsLog.OnDeleteScheduleCalled,
            action.ScheduleId, state.Schedules == null);

        if (state.Schedules == null)
        {
            Log.Warning(AppConstants.Logging.ScheduleCrudReducerDiagnosticsLog.OnDeleteScheduleSchedulesNullReturningUnchanged);
            return state;
        }

        Log.Information(AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnDeleteScheduleScheduleIdCurrentCount,
            action.ScheduleId, state.Schedules.Count);

        var newSchedules = new ObservableHashSet<ScheduleStateItem>();
        foreach (var scheduleItem in state.Schedules)
        {
            if (scheduleItem.Id != action.ScheduleId)
            {
                newSchedules.Add(scheduleItem);
            }
        }

        var updatedCurrentSchedule = state.CurrentSchedule?.Id == action.ScheduleId ? null : state.CurrentSchedule;
        return StateFactory.CreateStateWithSchedules(state, newSchedules, updatedCurrentSchedule);
    }

    public static ApplicationState OnCreateScheduleFailure(ApplicationState state, CreateScheduleFailureAction action)
    {
        if (action.Schedule == null || state.Schedules == null)
        {
            return state;
        }

        Log.Warning(AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnCreateScheduleFailure,
            action.Schedule.Id, action.Error);

        // Remove the optimistically added schedule
        var newSchedules = new ObservableHashSet<ScheduleStateItem>();
        foreach (var scheduleItem in state.Schedules)
        {
            // Remove if it matches the failed schedule (by ID or by temporary ID)
            if (scheduleItem.Id != action.Schedule.Id && scheduleItem.Id != -1)
            {
                newSchedules.Add(scheduleItem);
            }
        }

        var updatedCurrentSchedule = state.CurrentSchedule?.Id == action.Schedule.Id ? null : state.CurrentSchedule;
        return StateFactory.CreateStateWithSchedules(state, newSchedules, updatedCurrentSchedule);
    }

    public static ApplicationState OnDeleteScheduleFailure(ApplicationState state, DeleteScheduleFailureAction action)
    {
        Log.Warning(AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnDeleteScheduleFailure,
            action.ScheduleId, action.Error);

        // If we have the schedule data, restore it to rollback the optimistic update
        if (action.Schedule != null && state.Schedules != null)
        {
            Log.Information(AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnDeleteScheduleFailureRestoringSchedule, action.ScheduleId);

            // Check if schedule is already in the collection (shouldn't be, but check to avoid duplicates)
            var existingSchedule = state.Schedules.FirstOrDefault(s => s.Id == action.ScheduleId);
            if (existingSchedule == null)
            {
                // Create new collection with the restored schedule
                var newSchedules = new ObservableHashSet<ScheduleStateItem>();
                foreach (var scheduleItem in state.Schedules)
                {
                    newSchedules.Add(scheduleItem);
                }
                // Deep clone to ensure independence
                newSchedules.Add(action.Schedule.DeepClone());

                return StateFactory.CreateStateWithSchedules(state, newSchedules);
            }
            else
            {
                Log.Debug(AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnDeleteScheduleFailureScheduleAlreadyExistsSkippingRestoration, action.ScheduleId);
            }
        }
        else
        {
            Log.Warning(AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnDeleteScheduleFailureNoScheduleDataForRollback, action.ScheduleId);
        }

        return state;
    }

    public static ApplicationState OnCreateScheduleSuccess(ApplicationState state, CreateScheduleSuccessAction action)
    {
        Log.Information(AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnCreateScheduleSuccess,
            action.Schedule?.Id, action.Schedule?.Name);

        if (action.Schedule == null || state.Schedules == null)
        {
            return state;
        }

        // Remove optimistic schedule (by ID or temporary ID -1) and add confirmed one
        var newSchedules = new ObservableHashSet<ScheduleStateItem>();
        foreach (var scheduleItem in state.Schedules)
        {
            // Skip the optimistic schedule (will be replaced with confirmed one)
            if (scheduleItem.Id != action.Schedule.Id && scheduleItem.Id != -1)
            {
                newSchedules.Add(scheduleItem);
            }
        }
        // Add the confirmed schedule - deep clone to ensure independence from CurrentSchedule
        newSchedules.Add(action.Schedule.DeepClone());

        // Only set CurrentSchedule if it was already set to this schedule (don't set it if it was cleared)
        var updatedCurrentSchedule = state.CurrentSchedule?.Id == action.Schedule.Id
            ? action.Schedule.DeepClone()
            : state.CurrentSchedule;

        return StateFactory.CreateStateWithSchedules(state, newSchedules, updatedCurrentSchedule);
    }

    public static ApplicationState OnAddScheduleSuccess(ApplicationState state, AddScheduleSuccessAction action)
    {
        Log.Information(AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnAddScheduleSuccessWithExistingCount,
            action.Schedule?.Id, action.Schedule?.Name, state.Schedules?.Count ?? 0);

        var newSchedules = new ObservableHashSet<ScheduleStateItem>();
        if (state.Schedules != null)
        {
            foreach (var scheduleItem in state.Schedules)
            {
                newSchedules.Add(scheduleItem);
            }
        }
        // Add the DTO (already transformed by Effect)
        if (action.Schedule != null)
        {
            newSchedules.Add(action.Schedule);
        }

        Log.Information(AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnAddScheduleSuccessNewSchedulesCount, newSchedules.Count);

        // Deep clone to ensure CurrentSchedule is independent from the item in Schedules collection
        ScheduleStateItem? clonedCurrentSchedule = action.Schedule?.DeepClone();

        return StateFactory.CreateStateWithSchedules(state, newSchedules, clonedCurrentSchedule);
    }

    public static ApplicationState OnUpdateScheduleSuccess(ApplicationState state, UpdateScheduleSuccessAction action)
    {
        if (state.Schedules == null)
        {
            return state;
        }

        Log.Debug(AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnUpdateScheduleSuccessEntry,
            action.Schedule.Id,
            action.Schedule.BiblePublicationLanguageName ?? "null",
            action.Schedule.BiblePublicationSectionName ?? "null",
            action.Schedule.BiblePublicationTrackTitle ?? "null",
            action.Schedule.BiblePublicationCode ?? "null",
            action.Schedule.MusicEnabled);

        // Create new Schedules collection to maintain immutability
        var newSchedules = new ObservableHashSet<ScheduleStateItem>();
        ScheduleStateItem? updatedScheduleItem = null;

        if (state.Schedules != null)
        {
            foreach (var scheduleItem in state.Schedules)
            {
                if (scheduleItem.Id == action.Schedule.Id)
                {
                    // Create new schedule item with updated properties (immutable update)
                    var oldMusicEnabled = scheduleItem.MusicEnabled;
                    var sourceSchedule = action.Schedule.DeepClone();
                    // Use the cloned schedule directly
                    updatedScheduleItem = sourceSchedule;

                    Log.Debug(AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnUpdateScheduleSuccessExistingItem,
                        scheduleItem.BiblePublicationLanguageName ?? "null",
                        scheduleItem.BiblePublicationSectionName ?? "null",
                        scheduleItem.BiblePublicationTrackTitle ?? "null",
                        scheduleItem.BiblePublicationCode ?? "null",
                        scheduleItem.MusicEnabled);

                    // Preserve display names from existing schedule if they're missing in the action
                    // This ensures display names are available immediately after save, before bootstrap service populates them
                    DisplayNamePreservationHelper.PreserveDisplayNamesFromExisting(updatedScheduleItem, scheduleItem);

                    Log.Debug(AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnUpdateScheduleSuccessAfterPreservation,
                        updatedScheduleItem.BiblePublicationSectionName ?? "null",
                        updatedScheduleItem.BiblePublicationTrackTitle ?? "null",
                        updatedScheduleItem.BiblePublicationCode ?? "null");

                    Log.Debug(AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnUpdateScheduleSuccessUpdatedScheduleItem,
                        oldMusicEnabled, updatedScheduleItem.MusicEnabled, updatedScheduleItem.Name, action.Schedule.Name);

                    newSchedules.Add(updatedScheduleItem);
                }
                else
                {
                    // Keep existing schedule unchanged
                    newSchedules.Add(scheduleItem);
                }
            }
        }

        if (updatedScheduleItem == null)
        {
            // Schedule not found, add it (shouldn't happen, but handle gracefully) - deep clone for independence
            updatedScheduleItem = action.Schedule.DeepClone();
            newSchedules.Add(updatedScheduleItem);
            Log.Debug(AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnUpdateScheduleSuccessAddedNewScheduleItem);
        }

        // Update CurrentSchedule only if it matches the updated schedule (don't set it if it was cleared)
        ScheduleStateItem? updatedCurrentSchedule = state.CurrentSchedule;
        if (state.CurrentSchedule != null && state.CurrentSchedule.Id == action.Schedule.Id)
        {
            // Use the updated item from the new collection, but deep clone it for independence
            updatedCurrentSchedule = updatedScheduleItem?.DeepClone() ?? action.Schedule.DeepClone();
        }

        return new ApplicationState(
            schedules: newSchedules,
            currentSchedule: updatedCurrentSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible,
            containerReadiness: state.ContainerReadiness);
    }

    public static ApplicationState OnRemoveScheduleSuccess(ApplicationState state, RemoveScheduleSuccessAction action)
    {
        if (state.Schedules == null)
        {
            return state;
        }

        var newSchedules = new ObservableHashSet<ScheduleStateItem>();
        foreach (var scheduleItem in state.Schedules)
        {
            if (scheduleItem.Id != action.ScheduleId)
            {
                newSchedules.Add(scheduleItem);
            }
        }

        var updatedCurrentSchedule = state.CurrentSchedule?.Id == action.ScheduleId ? null : state.CurrentSchedule;
        return StateFactory.CreateStateWithSchedules(state, newSchedules, updatedCurrentSchedule);
    }
}

