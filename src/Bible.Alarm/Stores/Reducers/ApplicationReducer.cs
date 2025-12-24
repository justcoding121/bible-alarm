#nullable enable

using Bible.Alarm.Common.Extensions;
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
            BibleReadingLanguageName = action.Schedule.BibleReadingLanguageName,
            BibleReadingPublicationName = action.Schedule.BibleReadingPublicationName,
            MusicLanguageName = action.Schedule.MusicLanguageName,
            MusicPublicationName = action.Schedule.MusicPublicationName,
            MusicTrackName = action.Schedule.MusicTrackName
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

        LogUpdateStart(action);

        var existingScheduleItem = state.Schedules.FirstOrDefault(s => s.Id == action.Schedule.Id);
        if (existingScheduleItem != null)
        {
            PreserveDisplayNamesFromExisting(action.Schedule, existingScheduleItem);
            UpdateScheduleInCollection(state.Schedules, existingScheduleItem, action.Schedule);
        }
        else
        {
            state.Schedules.Add(action.Schedule.DeepClone());
        }

        var updatedCurrentSchedule = UpdateCurrentScheduleIfMatches(state, action.Schedule);
        var updatedCurrentBibleReadingSchedule = SyncBibleReadingScheduleIfNeeded(action, updatedCurrentSchedule);
        var updatedCurrentMusic = SyncMusicIfNeeded(action, updatedCurrentSchedule);

        return CreateUpdatedState(state, updatedCurrentSchedule, updatedCurrentMusic, updatedCurrentBibleReadingSchedule);
    }

    private static void LogUpdateStart(UpdateScheduleFromViewModelAction action)
    {
        Log.Information("ApplicationReducer: OnUpdateScheduleFromViewModel - ScheduleId: {ScheduleId}, Name: {Name}, LanguageCode: {LanguageCode}, LanguageName: {LanguageName}, PublicationCode: {PublicationCode}, PublicationName: {PublicationName}",
            action.Schedule!.Id, action.Schedule.Name,
            action.Schedule.BibleReadingLanguageCode ?? "null",
            action.Schedule.BibleReadingLanguageName ?? "null",
            action.Schedule.BibleReadingPublicationCode ?? "null",
            action.Schedule.BibleReadingPublicationName ?? "null");
    }

    private static void PreserveDisplayNamesFromExisting(ScheduleStateItem actionSchedule, ScheduleStateItem existingScheduleItem)
    {
        Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - Found existing schedule. Existing LanguageName: {ExistingLanguageName}, Action LanguageName: {ActionLanguageName}",
            existingScheduleItem.BibleReadingLanguageName ?? "null",
            actionSchedule.BibleReadingLanguageName ?? "null");

        PreserveBibleReadingDisplayNames(actionSchedule, existingScheduleItem);
        PreserveMusicDisplayNames(actionSchedule, existingScheduleItem);
    }

    private static void PreserveBibleReadingDisplayNames(ScheduleStateItem actionSchedule, ScheduleStateItem existingScheduleItem)
    {
        if (string.IsNullOrWhiteSpace(actionSchedule.BibleReadingLanguageName) && !string.IsNullOrWhiteSpace(existingScheduleItem.BibleReadingLanguageName))
        {
            Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - Preserving existing BibleReadingLanguageName: {LanguageName}",
                existingScheduleItem.BibleReadingLanguageName);
            actionSchedule.BibleReadingLanguageName = existingScheduleItem.BibleReadingLanguageName;
        }
        else
        {
            Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - Using action's BibleReadingLanguageName: {LanguageName}",
                actionSchedule.BibleReadingLanguageName ?? "null");
        }

        if (string.IsNullOrWhiteSpace(actionSchedule.BibleReadingPublicationName) && !string.IsNullOrWhiteSpace(existingScheduleItem.BibleReadingPublicationName))
        {
            actionSchedule.BibleReadingPublicationName = existingScheduleItem.BibleReadingPublicationName;
        }

        if (string.IsNullOrWhiteSpace(actionSchedule.BibleReadingBookName) && !string.IsNullOrWhiteSpace(existingScheduleItem.BibleReadingBookName))
        {
            actionSchedule.BibleReadingBookName = existingScheduleItem.BibleReadingBookName;
        }
    }

    private static void PreserveMusicDisplayNames(ScheduleStateItem actionSchedule, ScheduleStateItem existingScheduleItem)
    {
        if (string.IsNullOrWhiteSpace(actionSchedule.MusicLanguageName) && !string.IsNullOrWhiteSpace(existingScheduleItem.MusicLanguageName))
        {
            actionSchedule.MusicLanguageName = existingScheduleItem.MusicLanguageName;
        }

        if (string.IsNullOrWhiteSpace(actionSchedule.MusicPublicationName) && !string.IsNullOrWhiteSpace(existingScheduleItem.MusicPublicationName))
        {
            actionSchedule.MusicPublicationName = existingScheduleItem.MusicPublicationName;
        }

        if (string.IsNullOrWhiteSpace(actionSchedule.MusicTrackName) && !string.IsNullOrWhiteSpace(existingScheduleItem.MusicTrackName))
        {
            actionSchedule.MusicTrackName = existingScheduleItem.MusicTrackName;
        }
    }

    private static void UpdateScheduleInCollection(ObservableHashSet<ScheduleStateItem> schedules, ScheduleStateItem existingScheduleItem, ScheduleStateItem actionSchedule)
    {
        // Remove old and add updated - deep clone to ensure independence from CurrentSchedule
        schedules.Remove(existingScheduleItem);
        schedules.Add(actionSchedule.DeepClone());
    }

    private static ScheduleStateItem? UpdateCurrentScheduleIfMatches(ApplicationState state, ScheduleStateItem actionSchedule)
    {
        if (state.CurrentSchedule?.Id == actionSchedule.Id)
        {
            Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - Updating CurrentSchedule. New LanguageName: {LanguageName}, PublicationName: {PublicationName}",
                actionSchedule.BibleReadingLanguageName ?? "null",
                actionSchedule.BibleReadingPublicationName ?? "null");
            return actionSchedule.DeepClone();
        }

        Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - CurrentSchedule ID ({CurrentScheduleId}) doesn't match action Schedule ID ({ActionScheduleId}), not updating CurrentSchedule",
            state.CurrentSchedule?.Id ?? 0, actionSchedule.Id);
        return state.CurrentSchedule;
    }

    private static BibleReadingStateItem? SyncBibleReadingScheduleIfNeeded(UpdateScheduleFromViewModelAction action, ScheduleStateItem? updatedCurrentSchedule)
    {
        if (!action.BibleReadingUpdated || updatedCurrentSchedule == null)
        {
            return null;
        }

        if (!HasValidBibleReadingProperties(updatedCurrentSchedule))
        {
            return null;
        }

        var bibleReadingSchedule = CreateBibleReadingScheduleFromCurrent(updatedCurrentSchedule);
        Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - Synced CurrentBibleReadingSchedule from CurrentSchedule. LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}",
            bibleReadingSchedule.LanguageCode, bibleReadingSchedule.PublicationCode);
        return bibleReadingSchedule;
    }

    private static bool HasValidBibleReadingProperties(ScheduleStateItem schedule)
    {
        return !string.IsNullOrWhiteSpace(schedule.BibleReadingLanguageCode) &&
               !string.IsNullOrWhiteSpace(schedule.BibleReadingPublicationCode) &&
               schedule.BibleReadingBookNumber.HasValue &&
               schedule.BibleReadingBookNumber.Value > 0 &&
               schedule.BibleReadingChapterNumber.HasValue &&
               schedule.BibleReadingChapterNumber.Value > 0;
    }

    private static BibleReadingStateItem CreateBibleReadingScheduleFromCurrent(ScheduleStateItem updatedCurrentSchedule)
    {
        if (!updatedCurrentSchedule.BibleReadingBookNumber.HasValue || !updatedCurrentSchedule.BibleReadingChapterNumber.HasValue)
        {
            throw new InvalidOperationException("BibleReadingBookNumber and BibleReadingChapterNumber must have values");
        }
        return new BibleReadingStateItem
        {
            Id = updatedCurrentSchedule.BibleReadingScheduleId ?? 0,
            LanguageCode = updatedCurrentSchedule.BibleReadingLanguageCode ?? string.Empty,
            PublicationCode = updatedCurrentSchedule.BibleReadingPublicationCode ?? string.Empty,
            BookNumber = updatedCurrentSchedule.BibleReadingBookNumber.Value,
            ChapterNumber = updatedCurrentSchedule.BibleReadingChapterNumber.Value,
            FinishedDuration = updatedCurrentSchedule.BibleReadingFinishedDuration ?? TimeSpan.Zero,
            AlarmScheduleId = updatedCurrentSchedule.Id,
            TranslationName = updatedCurrentSchedule.BibleReadingPublicationName ?? string.Empty
        };
    }

    private static MusicStateItem? SyncMusicIfNeeded(UpdateScheduleFromViewModelAction action, ScheduleStateItem? updatedCurrentSchedule)
    {
        if (!action.MusicUpdated || updatedCurrentSchedule == null)
        {
            return null;
        }

        if (!HasValidMusicProperties(updatedCurrentSchedule))
        {
            return null;
        }

        var music = CreateMusicFromCurrent(updatedCurrentSchedule);
        Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - Synced CurrentMusic from CurrentSchedule. MusicType: {MusicType}, LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}",
            music.MusicType, music.LanguageCode ?? "null", music.PublicationCode);
        return music;
    }

    private static bool HasValidMusicProperties(ScheduleStateItem schedule)
    {
        return schedule.MusicType.HasValue &&
               !string.IsNullOrWhiteSpace(schedule.MusicPublicationCode) &&
               schedule.MusicTrackNumber.HasValue &&
               schedule.MusicTrackNumber.Value > 0;
    }

    private static MusicStateItem CreateMusicFromCurrent(ScheduleStateItem updatedCurrentSchedule)
    {
        if (!updatedCurrentSchedule.MusicType.HasValue || !updatedCurrentSchedule.MusicTrackNumber.HasValue)
        {
            throw new InvalidOperationException("MusicType and MusicTrackNumber must have values");
        }
        return new MusicStateItem
        {
            Id = updatedCurrentSchedule.MusicId ?? 0,
            MusicType = updatedCurrentSchedule.MusicType.Value,
            PublicationCode = updatedCurrentSchedule.MusicPublicationCode ?? string.Empty,
            LanguageCode = updatedCurrentSchedule.MusicLanguageCode ?? string.Empty,
            TrackNumber = updatedCurrentSchedule.MusicTrackNumber.Value,
            Repeat = updatedCurrentSchedule.MusicRepeat ?? false,
            AlarmScheduleId = updatedCurrentSchedule.Id,
            LanguageName = updatedCurrentSchedule.MusicLanguageName,
            PublicationName = updatedCurrentSchedule.MusicPublicationName,
            TrackName = updatedCurrentSchedule.MusicTrackName
        };
    }

    private static ApplicationState CreateUpdatedState(
        ApplicationState state,
        ScheduleStateItem? updatedCurrentSchedule,
        MusicStateItem? updatedCurrentMusic,
        BibleReadingStateItem? updatedCurrentBibleReadingSchedule)
    {
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: updatedCurrentSchedule,
            currentMusic: updatedCurrentMusic,
            currentBibleReadingSchedule: updatedCurrentBibleReadingSchedule,
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
    /// </summary>
    [ReducerMethod]
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

                return new ApplicationState(
                    schedules: newSchedules,
                    currentSchedule: state.CurrentSchedule,
                    currentMusic: state.CurrentMusic,
                    currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
                    isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
                    isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
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
        // Add the confirmed schedule - deep clone to ensure independence from CurrentSchedule
        newSchedules.Add(action.Schedule.DeepClone());

        // Only set CurrentSchedule if it was already set to this schedule (don't set it if it was cleared)
        // This prevents CurrentSchedule from being set after save/cancel when it was intentionally cleared
        // Deep clone to ensure CurrentSchedule is independent from the item in Schedules collection
        var updatedCurrentSchedule = state.CurrentSchedule?.Id == action.Schedule.Id 
            ? action.Schedule.DeepClone() 
            : state.CurrentSchedule;

        return new ApplicationState(
            schedules: newSchedules,
            currentSchedule: updatedCurrentSchedule,
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

        // Deep clone to ensure CurrentSchedule is independent from the item in Schedules collection
        ScheduleStateItem? clonedCurrentSchedule = action.Schedule?.DeepClone();

        return new ApplicationState(
            schedules: newSchedules,
            // Set CurrentSchedule to the newly added schedule (deep cloned for independence)
            currentSchedule: clonedCurrentSchedule,
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
            // Deep clone to ensure independence from CurrentSchedule
            state.Schedules.Remove(existingScheduleItem);
            state.Schedules.Add(action.Schedule.DeepClone());

            Log.Debug("ApplicationReducer: Replaced schedule item in state");
        }
        else
        {
            // Schedule not found, add it (shouldn't happen, but handle gracefully) - deep clone for independence
            state.Schedules.Add(action.Schedule.DeepClone());
            Log.Debug("ApplicationReducer: Added new schedule item to state");
        }

        // Update CurrentSchedule only if it matches the updated schedule (don't set it if it was cleared)
        // This prevents CurrentSchedule from being set after save/cancel when it was intentionally cleared
        // Deep clone to ensure CurrentSchedule is independent from the item in Schedules collection
        ScheduleStateItem? updatedCurrentSchedule = state.CurrentSchedule;
        if (state.CurrentSchedule != null && state.CurrentSchedule.Id == action.Schedule.Id)
        {
            // Use the updated item from the collection if it exists, but deep clone it
            var updatedItemFromCollection = state.Schedules.FirstOrDefault(s => s.Id == action.Schedule.Id);
            updatedCurrentSchedule = (updatedItemFromCollection ?? action.Schedule).DeepClone();
        }
        // If CurrentSchedule is null, keep it null (it was intentionally cleared)

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
        // Create CurrentBibleReadingSchedule from schedule's Bible reading properties
        BibleReadingStateItem? currentBibleReadingSchedule = null;
        if (action.SelectedSchedule != null &&
            !string.IsNullOrWhiteSpace(action.SelectedSchedule.BibleReadingLanguageCode) &&
            !string.IsNullOrWhiteSpace(action.SelectedSchedule.BibleReadingPublicationCode) &&
            action.SelectedSchedule.BibleReadingBookNumber.HasValue &&
            action.SelectedSchedule.BibleReadingBookNumber.Value > 0 &&
            action.SelectedSchedule.BibleReadingChapterNumber.HasValue &&
            action.SelectedSchedule.BibleReadingChapterNumber.Value > 0)
        {
            currentBibleReadingSchedule = new BibleReadingStateItem
            {
                Id = action.SelectedSchedule.BibleReadingScheduleId ?? 0,
                LanguageCode = action.SelectedSchedule.BibleReadingLanguageCode,
                PublicationCode = action.SelectedSchedule.BibleReadingPublicationCode,
                BookNumber = action.SelectedSchedule.BibleReadingBookNumber.Value,
                ChapterNumber = action.SelectedSchedule.BibleReadingChapterNumber.Value,
                FinishedDuration = action.SelectedSchedule.BibleReadingFinishedDuration ?? TimeSpan.Zero,
                AlarmScheduleId = action.SelectedSchedule.Id,
                TranslationName = action.SelectedSchedule.BibleReadingPublicationName
            };
        }

        // Deep clone the selected schedule to ensure CurrentSchedule is independent from the item in Schedules collection
        ScheduleStateItem? clonedCurrentSchedule = action.SelectedSchedule?.DeepClone();

        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: clonedCurrentSchedule,
            currentMusic: state.CurrentMusic,
            currentBibleReadingSchedule: currentBibleReadingSchedule,
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
        Serilog.Log.Debug("ApplicationReducer: OnChapterSelected - Updating CurrentBibleReadingSchedule. LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}, BookNumber: {BookNumber}, ChapterNumber: {ChapterNumber}",
            action.CurrentBibleReadingSchedule?.LanguageCode ?? "null",
            action.CurrentBibleReadingSchedule?.PublicationCode ?? "null",
            action.CurrentBibleReadingSchedule?.BookNumber ?? 0,
            action.CurrentBibleReadingSchedule?.ChapterNumber ?? 0);

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

