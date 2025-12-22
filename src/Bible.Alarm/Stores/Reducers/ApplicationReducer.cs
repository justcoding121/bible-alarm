#nullable enable

using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Stores.Reducers;

public static class ApplicationReducer
{
    [ReducerMethod]
    public static ApplicationState OnInitialize(ApplicationState state, InitializeAction action)
    {
        return new ApplicationState(
            schedules: action.ScheduleList,
            currentSchedule: null,
            currentMusic: null,
            currentBibleReadingSchedule: null,
            isHomePageOverlayVisible: false,
            isSchedulePageOverlayVisible: false);
    }

    /// <summary>
    /// Optimistic reducer: Handle CreateScheduleAction - immediately add to store for fast UI feedback.
    /// Following Fluxor best practices: Optimistic updates for responsive UX.
    /// </summary>
    [ReducerMethod]
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
        // Mark as pending if needed (could add IsSaving flag to ScheduleStateItem if needed)
        // Create a new instance since ScheduleStateItem is a class, not a record
        var optimisticSchedule = new ScheduleStateItem
        {
            Id = action.Schedule.Id == 0 ? -1 : action.Schedule.Id,
            Name = action.Schedule.Name,
            IsEnabled = action.Schedule.IsEnabled,
            Hour = action.Schedule.Hour,
            Minute = action.Schedule.Minute,
            Second = action.Schedule.Second,
            DaysOfWeek = action.Schedule.DaysOfWeek,
            NotificationEnabled = action.Schedule.NotificationEnabled,
            MusicEnabled = action.Schedule.MusicEnabled,
            SnoozeMinutes = action.Schedule.SnoozeMinutes,
            NumberOfChaptersToRead = action.Schedule.NumberOfChaptersToRead,
            AlwaysPlayFromStart = action.Schedule.AlwaysPlayFromStart,
            CurrentPlayItem = action.Schedule.CurrentPlayItem,
            LatestAlarmNotificationId = action.Schedule.LatestAlarmNotificationId,
            BibleReadingScheduleId = action.Schedule.BibleReadingScheduleId,
            BibleReadingLanguageCode = action.Schedule.BibleReadingLanguageCode,
            BibleReadingPublicationCode = action.Schedule.BibleReadingPublicationCode,
            BibleReadingBookNumber = action.Schedule.BibleReadingBookNumber,
            BibleReadingChapterNumber = action.Schedule.BibleReadingChapterNumber,
            BibleReadingFinishedDuration = action.Schedule.BibleReadingFinishedDuration,
            MusicId = action.Schedule.MusicId,
            MusicType = action.Schedule.MusicType,
            MusicPublicationCode = action.Schedule.MusicPublicationCode,
            MusicLanguageCode = action.Schedule.MusicLanguageCode,
            MusicTrackNumber = action.Schedule.MusicTrackNumber,
            MusicRepeat = action.Schedule.MusicRepeat,
            BibleReadingLanguageName = action.Schedule.BibleReadingLanguageName
        };
        newSchedules.Add(optimisticSchedule);

        Log.Information("ApplicationReducer: OnCreateSchedule - NewSchedulesCount: {NewCount}", newSchedules.Count);

        return new ApplicationState(
            schedules: newSchedules,
            currentSchedule: optimisticSchedule,
            currentMusic: state.CurrentMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    /// <summary>
    /// Optimistic reducer: Handle UpdateScheduleFromViewModelAction - immediately update in store for fast UI feedback.
    /// Following Fluxor best practices: Optimistic updates for responsive UX.
    /// </summary>
    [ReducerMethod]
    public static ApplicationState OnUpdateScheduleFromViewModel(ApplicationState state, UpdateScheduleFromViewModelAction action)
    {
        if (action.Schedule == null || state.Schedules == null)
        {
            return state;
        }

        Log.Information("ApplicationReducer: OnUpdateScheduleFromViewModel - ScheduleId: {ScheduleId}, Name: {Name}",
            action.Schedule.Id, action.Schedule.Name);

        // Find and update the existing schedule optimistically
        var existingScheduleItem = state.Schedules.FirstOrDefault(s => s.Id == action.Schedule.Id);

        if (existingScheduleItem != null)
        {
            // Preserve BibleReadingLanguageName and BibleReadingBookName from existing item (they're not in the action's ScheduleStateItem)
            // because they're populated during bootstrap/effects, not stored in database
            if (string.IsNullOrWhiteSpace(action.Schedule.BibleReadingLanguageName) && !string.IsNullOrWhiteSpace(existingScheduleItem.BibleReadingLanguageName))
            {
                action.Schedule.BibleReadingLanguageName = existingScheduleItem.BibleReadingLanguageName;
            }
            if (string.IsNullOrWhiteSpace(action.Schedule.BibleReadingBookName) && !string.IsNullOrWhiteSpace(existingScheduleItem.BibleReadingBookName))
            {
                action.Schedule.BibleReadingBookName = existingScheduleItem.BibleReadingBookName;
            }

            // Remove old and add updated
            state.Schedules.Remove(existingScheduleItem);
            state.Schedules.Add(action.Schedule);
        }
        else
        {
            // Schedule not found, add it (shouldn't happen, but handle gracefully)
            state.Schedules.Add(action.Schedule);
        }

        // Update CurrentSchedule if it matches
        ScheduleStateItem? updatedCurrentSchedule = state.CurrentSchedule;
        if (state.CurrentSchedule?.Id == action.Schedule.Id)
        {
            updatedCurrentSchedule = action.Schedule;
        }

        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: updatedCurrentSchedule,
            currentMusic: state.CurrentMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    /// <summary>
    /// Optimistic reducer: Handle DeleteScheduleAction - immediately remove from store for fast UI feedback.
    /// Following Fluxor best practices: Optimistic updates for responsive UX.
    /// </summary>
    [ReducerMethod]
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

        return new ApplicationState(
            schedules: newSchedules,
            currentSchedule: state.CurrentSchedule?.Id == action.ScheduleId ? null : state.CurrentSchedule,
            currentMusic: state.CurrentMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    /// <summary>
    /// Failure reducer: Handle CreateScheduleFailureAction - rollback optimistic update.
    /// Following Fluxor best practices: Rollback optimistic changes on failure.
    /// </summary>
    [ReducerMethod]
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

        return new ApplicationState(
            schedules: newSchedules,
            currentSchedule: state.CurrentSchedule?.Id == action.Schedule.Id ? null : state.CurrentSchedule,
            currentMusic: state.CurrentMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    /// <summary>
    /// Failure reducer: Handle UpdateScheduleFailureAction - rollback optimistic update.
    /// Following Fluxor best practices: Rollback optimistic changes on failure.
    /// Note: This is a simplified rollback - in a production app, you might want to store the previous state.
    /// </summary>
    [ReducerMethod]
    public static ApplicationState OnUpdateScheduleFailure(ApplicationState state, UpdateScheduleFailureAction action)
    {
        Log.Warning("ApplicationReducer: OnUpdateScheduleFailure - ScheduleId: {ScheduleId}, Error: {Error}",
            action.Schedule?.Id, action.Error);

        // For update failures, we could reload from server or keep the optimistic update
        // For now, we'll keep the optimistic update and let the user retry
        // In a production app, you might want to dispatch a reload action
        return state;
    }

    /// <summary>
    /// Failure reducer: Handle DeleteScheduleFailureAction - rollback optimistic update by re-adding the schedule.
    /// Following Fluxor best practices: Rollback optimistic changes on failure.
    /// Note: This requires fetching the schedule from DB or storing it before deletion.
    /// </summary>
    [ReducerMethod]
    public static ApplicationState OnDeleteScheduleFailure(ApplicationState state, DeleteScheduleFailureAction action)
    {
        Log.Warning("ApplicationReducer: OnDeleteScheduleFailure - ScheduleId: {ScheduleId}, Error: {Error}",
            action.ScheduleId, action.Error);

        // For delete failures, we can't easily rollback without the original schedule data
        // In a production app, you might want to dispatch a reload action to refresh from server
        // For now, we'll just log the error - the schedule was already removed optimistically
        return state;
    }

    /// <summary>
    /// Success reducer: Handle CreateScheduleSuccessAction - replace optimistic update with confirmed data.
    /// Following Fluxor best practices: Confirm optimistic updates with server data.
    /// </summary>
    [ReducerMethod]
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
        // Add the confirmed schedule
        newSchedules.Add(action.Schedule);

        return new ApplicationState(
            schedules: newSchedules,
            currentSchedule: action.Schedule,
            currentMusic: state.CurrentMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    /// <summary>
    /// Pure reducer: Handle AddScheduleSuccessAction with DTO (no mapping, no side effects).
    /// Following Fluxor best practices: Effects handle transformation, Reducers are pure.
    /// This is kept for backward compatibility with old AddScheduleAction flow.
    /// </summary>
    [ReducerMethod]
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

        return new ApplicationState(
            schedules: newSchedules,
            // Set CurrentSchedule to the newly added schedule (already a DTO)
            currentSchedule: action.Schedule,
            currentMusic: state.CurrentMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    /// <summary>
    /// Pure reducer: Handle RemoveScheduleSuccessAction (no mapping, no side effects).
    /// Following Fluxor best practices: Effects handle transformation, Reducers are pure.
    /// </summary>
    [ReducerMethod]
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

        return new ApplicationState(
            schedules: newSchedules,
            currentSchedule: state.CurrentSchedule?.Id == action.ScheduleId ? null : state.CurrentSchedule,
            currentMusic: state.CurrentMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    /// <summary>
    /// Pure reducer: Handle UpdateScheduleSuccessAction with DTO (no mapping, no side effects).
    /// Following Fluxor best practices: Effects handle transformation, Reducers are pure.
    /// </summary>
    [ReducerMethod]
    public static ApplicationState OnUpdateScheduleSuccess(ApplicationState state, UpdateScheduleSuccessAction action)
    {
        if (state.Schedules == null)
        {
            return state;
        }

        // Update the existing collection in place to avoid creating a new collection reference
        // This prevents the entire list from reloading when only one item is updated
        var existingScheduleItem = state.Schedules.FirstOrDefault(s => s.Id == action.Schedule.Id);

        Log.Debug("ApplicationReducer: OnUpdateScheduleSuccess - ScheduleId: {ScheduleId}, BibleReadingLanguageName: '{BibleReadingLanguageName}', BibleReadingBookName: '{BibleReadingBookName}'",
            action.Schedule.Id, action.Schedule.BibleReadingLanguageName ?? "null", action.Schedule.BibleReadingBookName ?? "null");

        if (existingScheduleItem != null)
        {
            Log.Debug("ApplicationReducer: Existing item BibleReadingLanguageName: '{BibleReadingLanguageName}', BibleReadingBookName: '{BibleReadingBookName}'",
                existingScheduleItem.BibleReadingLanguageName ?? "null", existingScheduleItem.BibleReadingBookName ?? "null");

            // Always replace the existing item with the updated one from the action
            // This ensures BibleReadingLanguageName and BibleReadingBookName are updated even if other properties haven't changed
            state.Schedules.Remove(existingScheduleItem);
            state.Schedules.Add(action.Schedule);

            Log.Debug("ApplicationReducer: Replaced schedule item in state");
        }
        else
        {
            // Schedule not found, add it (shouldn't happen, but handle gracefully)
            state.Schedules.Add(action.Schedule);
            Log.Debug("ApplicationReducer: Added new schedule item to state");
        }

        // Update CurrentSchedule if it matches the updated schedule
        ScheduleStateItem? updatedCurrentSchedule = state.CurrentSchedule;
        if (state.CurrentSchedule?.Id == action.Schedule.Id)
        {
            // Use the updated item from the collection if it exists
            var updatedItemFromCollection = state.Schedules.FirstOrDefault(s => s.Id == action.Schedule.Id);
            updatedCurrentSchedule = updatedItemFromCollection ?? action.Schedule;
        }

        return new ApplicationState(
            // Reuse the same collection reference
            schedules: state.Schedules,
            currentSchedule: updatedCurrentSchedule,
            currentMusic: state.CurrentMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    [ReducerMethod]
    public static ApplicationState OnViewSchedule(ApplicationState state, ViewScheduleAction action)
    {
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: action.SelectedSchedule,
            currentMusic: state.CurrentMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    [ReducerMethod]
    public static ApplicationState OnBack(ApplicationState state, BackAction action)
    {
        action.CurrentViewModel?.Dispose();
        // Preserve overlay state on back navigation
        return state;
    }

    [ReducerMethod]
    public static ApplicationState OnMusicSelection(ApplicationState state, MusicSelectionAction action)
    {
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: state.CurrentSchedule,
            currentMusic: action.CurrentMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    [ReducerMethod]
    public static ApplicationState OnSongBookSelection(ApplicationState state, SongBookSelectionAction action)
    {
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: state.CurrentSchedule,
            currentMusic: action.CurrentMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    [ReducerMethod]
    public static ApplicationState OnTrackSelection(ApplicationState state, TrackSelectionAction action)
    {
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: state.CurrentSchedule,
            currentMusic: action.CurrentMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    [ReducerMethod]
    public static ApplicationState OnTrackSelected(ApplicationState state, TrackSelectedAction action)
    {
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: state.CurrentSchedule,
            currentMusic: action.CurrentMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    [ReducerMethod]
    public static ApplicationState OnBibleSelection(ApplicationState state, BibleSelectionAction action)
    {
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: state.CurrentSchedule,
            currentMusic: state.CurrentMusic,
            currentBibleReadingSchedule: action.CurrentBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    [ReducerMethod]
    public static ApplicationState OnBookSelection(ApplicationState state, BookSelectionAction action)
    {
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: state.CurrentSchedule,
            currentMusic: state.CurrentMusic,
            currentBibleReadingSchedule: action.CurrentBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    [ReducerMethod]
    public static ApplicationState OnChapterSelection(ApplicationState state, ChapterSelectionAction action)
    {
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: state.CurrentSchedule,
            currentMusic: state.CurrentMusic,
            currentBibleReadingSchedule: action.CurrentBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    [ReducerMethod]
    public static ApplicationState OnChapterSelected(ApplicationState state, ChapterSelectedAction action)
    {
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: state.CurrentSchedule,
            currentMusic: state.CurrentMusic,
            currentBibleReadingSchedule: action.CurrentBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    [ReducerMethod]
    public static ApplicationState OnSetHomePageOverlay(ApplicationState state, SetHomePageOverlayAction action)
    {
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: state.CurrentSchedule,
            currentMusic: state.CurrentMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            isHomePageOverlayVisible: action.IsVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    [ReducerMethod]
    public static ApplicationState OnSetSchedulePageOverlay(ApplicationState state, SetSchedulePageOverlayAction action)
    {
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: state.CurrentSchedule,
            currentMusic: state.CurrentMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: action.IsVisible);
    }

    [ReducerMethod]
    public static ApplicationState OnResetScheduleState(ApplicationState state, ResetScheduleStateAction action)
    {
        // Reset all schedule-related state when navigating back to home
        // This ensures only one schedule is in state at any time
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: null,
            currentMusic: null,
            currentBibleReadingSchedule: null,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }
}

