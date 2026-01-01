#nullable enable
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Serilog;

namespace Bible.Alarm.Stores.Reducers.ApplicationReducer;

/// <summary>
/// Handles schedule CRUD operations (Create, Update, Delete, Success, Failure).
/// Separated from ApplicationReducer for better modularity.
/// </summary>
public static class ScheduleCrudReducer
{
    public static ApplicationState OnCreateSchedule(ApplicationState state, CreateScheduleAction action)
    {
        Log.Information("ApplicationReducer: OnCreateSchedule - Name: {Name}, ExistingSchedulesCount: {ExistingCount}",
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

        Log.Information("ApplicationReducer: OnCreateSchedule - NewSchedulesCount: {NewCount}", newSchedules.Count);

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
            NumberOfChaptersToRead = actionSchedule.NumberOfChaptersToRead,
            AlwaysPlayFromStart = actionSchedule.AlwaysPlayFromStart,
            CurrentPlayItem = actionSchedule.CurrentPlayItem,
            LatestAlarmNotificationId = actionSchedule.LatestAlarmNotificationId,
            BibleReadingScheduleId = actionSchedule.BibleReadingScheduleId,
            BibleReadingLanguageCode = actionSchedule.BibleReadingLanguageCode,
            BibleReadingPublicationCode = actionSchedule.BibleReadingPublicationCode,
            BibleReadingBookNumber = actionSchedule.BibleReadingBookNumber,
            BibleReadingChapterNumber = actionSchedule.BibleReadingChapterNumber,
            BibleReadingFinishedDuration = actionSchedule.BibleReadingFinishedDuration,
            MusicId = actionSchedule.MusicId,
            MusicType = actionSchedule.MusicType,
            MusicPublicationCode = actionSchedule.MusicPublicationCode,
            MusicLanguageCode = actionSchedule.MusicLanguageCode,
            MusicTrackNumber = actionSchedule.MusicTrackNumber,
            MusicRepeat = actionSchedule.MusicRepeat,
            BibleReadingLanguageName = actionSchedule.BibleReadingLanguageName,
            BibleReadingPublicationName = actionSchedule.BibleReadingPublicationName,
            MusicLanguageName = actionSchedule.MusicLanguageName,
            MusicPublicationName = actionSchedule.MusicPublicationName,
            MusicTrackName = actionSchedule.MusicTrackName
        };
    }

    public static ApplicationState OnDeleteSchedule(ApplicationState state, DeleteScheduleAction action)
    {
        if (state.Schedules == null)
        {
            return state;
        }

        Log.Information("ApplicationReducer: OnDeleteSchedule - ScheduleId: {ScheduleId}", action.ScheduleId);

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

        Log.Warning("ApplicationReducer: OnCreateScheduleFailure - ScheduleId: {ScheduleId}, Error: {Error}",
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
        Log.Warning("ApplicationReducer: OnDeleteScheduleFailure - ScheduleId: {ScheduleId}, Error: {Error}",
            action.ScheduleId, action.Error);

        // If we have the schedule data, restore it to rollback the optimistic update
        if (action.Schedule != null && state.Schedules != null)
        {
            Log.Information("ApplicationReducer: OnDeleteScheduleFailure - Restoring schedule {ScheduleId} to rollback optimistic deletion", action.ScheduleId);

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
                Log.Debug("ApplicationReducer: OnDeleteScheduleFailure - Schedule {ScheduleId} already exists in state, skipping restoration", action.ScheduleId);
            }
        }
        else
        {
            Log.Warning("ApplicationReducer: OnDeleteScheduleFailure - No schedule data provided for rollback, ScheduleId: {ScheduleId}", action.ScheduleId);
        }

        return state;
    }

    public static ApplicationState OnCreateScheduleSuccess(ApplicationState state, CreateScheduleSuccessAction action)
    {
        Log.Information("ApplicationReducer: OnCreateScheduleSuccess - ScheduleId: {ScheduleId}, Name: {Name}",
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
        Log.Information("ApplicationReducer: OnAddScheduleSuccess - ScheduleId: {ScheduleId}, Name: {Name}, ExistingSchedulesCount: {ExistingCount}",
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

        Log.Information("ApplicationReducer: OnAddScheduleSuccess - NewSchedulesCount: {NewCount}", newSchedules.Count);

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

        Log.Debug("ApplicationReducer: OnUpdateScheduleSuccess - ScheduleId: {ScheduleId}, BibleReadingLanguageName: '{BibleReadingLanguageName}', BibleReadingBookName: '{BibleReadingBookName}', MusicEnabled: {MusicEnabled}",
            action.Schedule.Id, action.Schedule.BibleReadingLanguageName ?? "null", action.Schedule.BibleReadingBookName ?? "null", action.Schedule.MusicEnabled);

        var existingScheduleItem = state.Schedules.FirstOrDefault(s => s.Id == action.Schedule.Id);

        if (existingScheduleItem != null)
        {
            Log.Debug("ApplicationReducer: Existing item BibleReadingLanguageName: '{BibleReadingLanguageName}', BibleReadingBookName: '{BibleReadingBookName}', MusicEnabled: {MusicEnabled}",
                existingScheduleItem.BibleReadingLanguageName ?? "null", existingScheduleItem.BibleReadingBookName ?? "null", existingScheduleItem.MusicEnabled);

            // Update the existing item in place to avoid Remove/Add sequence that causes Android Auto to remove the item
            var sourceSchedule = action.Schedule.DeepClone();
            var oldMusicEnabled = existingScheduleItem.MusicEnabled;
            SchedulePropertyCopier.CopyScheduleProperties(existingScheduleItem, sourceSchedule);

            Log.Debug("ApplicationReducer: Updated schedule item in place - MusicEnabled: {OldMusicEnabled} -> {NewMusicEnabled}, Name: '{OldName}' -> '{NewName}'",
                oldMusicEnabled, existingScheduleItem.MusicEnabled, existingScheduleItem.Name, action.Schedule.Name);
        }
        else
        {
            // Schedule not found, add it (shouldn't happen, but handle gracefully) - deep clone for independence
            state.Schedules.Add(action.Schedule.DeepClone());
            Log.Debug("ApplicationReducer: Added new schedule item to state");
        }

        // Update CurrentSchedule only if it matches the updated schedule (don't set it if it was cleared)
        ScheduleStateItem? updatedCurrentSchedule = state.CurrentSchedule;
        if (state.CurrentSchedule != null && state.CurrentSchedule.Id == action.Schedule.Id)
        {
            // Use the updated item from the collection if it exists, but deep clone it
            var updatedItemFromCollection = state.Schedules.FirstOrDefault(s => s.Id == action.Schedule.Id);
            updatedCurrentSchedule = (updatedItemFromCollection ?? action.Schedule).DeepClone();
        }

        return StateFactory.CreateUpdatedState(
            state,
            updatedCurrentSchedule,
            state.CurrentMusic,
            state.CurrentBibleReadingSchedule);
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

