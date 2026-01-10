#nullable enable

using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Stores.Reducers.Services;
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
        return ScheduleCrudReducer.OnCreateSchedule(state, action);
    }

    /// <summary>
    /// Optimistic reducer: Handle UpdateScheduleFromViewModelAction - immediately update in store for fast UI feedback.
    /// Following Fluxor best practices: Optimistic updates for responsive UX.
    /// 
    /// IMPORTANT: When shouldSave is false, only update CurrentSchedule, not the Schedules collection.
    /// This prevents Android Auto from detecting unsaved changes (like typing in the name field).
    /// The Schedules collection should only be updated when changes are saved (shouldSave: true).
    /// </summary>
    [ReducerMethod]
    public static ApplicationState OnUpdateScheduleFromViewModel(ApplicationState state, UpdateScheduleFromViewModelAction action)
    {
        if (action.Schedule == null || state.Schedules == null)
        {
            return state;
        }

        LogUpdateStart(action);

        // Only update the Schedules collection if shouldSave is true (changes are being saved)
        // When shouldSave is false, only update CurrentSchedule to avoid triggering Android Auto updates
        // IMPORTANT: Create new collection to maintain immutability
        ObservableHashSet<ScheduleStateItem>? newSchedules = null;
        if (action.ShouldSave)
        {
            newSchedules = new ObservableHashSet<ScheduleStateItem>();
            if (state.Schedules != null)
            {
                foreach (var scheduleItem in state.Schedules)
                {
                    if (scheduleItem.Id == action.Schedule.Id)
                    {
                        // Create new schedule item with updated properties (immutable update)
                        DisplayNamePreservationHelper.PreserveDisplayNamesFromExisting(action.Schedule, scheduleItem);
                        var updatedSchedule = action.Schedule.DeepClone();
                        newSchedules.Add(updatedSchedule);
                    }
                    else
                    {
                        // Keep existing schedule unchanged
                        newSchedules.Add(scheduleItem);
                    }
                }
            }

            // If schedule not found in collection and has valid ID, add it
            if (!newSchedules.Any(s => s.Id == action.Schedule.Id))
            {
                // Only add to Schedules collection if the schedule has a valid ID (Id > 0)
                // Unsaved schedules (Id=0) should not be in the Schedules collection
                // They should only exist in CurrentSchedule until saved
                if (action.Schedule.Id > 0)
                {
                    newSchedules.Add(action.Schedule.DeepClone());
                }
                else
                {
                    Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - Skipping add to Schedules collection for unsaved schedule (Id=0). Schedule should only exist in CurrentSchedule until saved.");
                }
            }
        }
        // else: ShouldSave=false means only update CurrentSchedule, not Schedules collection

        var updatedCurrentSchedule = ScheduleStateSyncHelper.UpdateCurrentScheduleIfMatches(state, action.Schedule);
        var updatedCurrentBibleReadingSchedule = ScheduleStateSyncHelper.SyncBibleReadingScheduleIfNeeded(action, updatedCurrentSchedule);
        var updatedCurrentMusic = ScheduleStateSyncHelper.SyncMusicIfNeeded(action, updatedCurrentSchedule);

        // Create new state with new Schedules collection if it was updated, otherwise use existing
        if (newSchedules != null)
        {
            return new ApplicationState(
                schedules: newSchedules,
                currentSchedule: updatedCurrentSchedule,
                currentMusic: updatedCurrentMusic,
                currentBibleReadingSchedule: updatedCurrentBibleReadingSchedule,
                isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
                isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible,
                containerReadiness: state.ContainerReadiness);
        }
        else
        {
            return StateFactory.CreateUpdatedState(state, updatedCurrentSchedule, updatedCurrentMusic, updatedCurrentBibleReadingSchedule);
        }
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

    // REMOVED: UpdateScheduleInCollection - This method was mutating existing state objects.
    // All state updates now create new instances to maintain immutability.

    /// <summary>
    /// Optimistic reducer: Handle DeleteScheduleAction - immediately remove from store for fast UI feedback.
    /// Following Fluxor best practices: Optimistic updates for responsive UX.
    /// </summary>
    [ReducerMethod]
    public static ApplicationState OnDeleteSchedule(ApplicationState state, DeleteScheduleAction action)
    {
        if (action == null)
        {
            return state;
        }
        Log.Information("ApplicationReducer: OnDeleteSchedule REDUCER CALLED - ScheduleId: {ScheduleId}, Action type: {ActionType}", 
            action.ScheduleId, action.GetType().FullName);
        return ScheduleCrudReducer.OnDeleteSchedule(state, action);
    }

    /// <summary>
    /// Failure reducer: Handle CreateScheduleFailureAction - rollback optimistic update.
    /// Following Fluxor best practices: Rollback optimistic changes on failure.
    /// </summary>
    [ReducerMethod]
    public static ApplicationState OnCreateScheduleFailure(ApplicationState state, CreateScheduleFailureAction action)
    {
        return ScheduleCrudReducer.OnCreateScheduleFailure(state, action);
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
        return ScheduleCrudReducer.OnDeleteScheduleFailure(state, action);
    }

    /// <summary>
    /// Success reducer: Handle CreateScheduleSuccessAction - replace optimistic update with confirmed data.
    /// Following Fluxor best practices: Confirm optimistic updates with server data.
    /// </summary>
    [ReducerMethod]
    public static ApplicationState OnCreateScheduleSuccess(ApplicationState state, CreateScheduleSuccessAction action)
    {
        return ScheduleCrudReducer.OnCreateScheduleSuccess(state, action);
    }

    /// <summary>
    /// Pure reducer: Handle AddScheduleSuccessAction with DTO (no mapping, no side effects).
    /// Following Fluxor best practices: Effects handle transformation, Reducers are pure.
    /// This is kept for backward compatibility with old AddScheduleAction flow.
    /// </summary>
    [ReducerMethod]
    public static ApplicationState OnAddScheduleSuccess(ApplicationState state, AddScheduleSuccessAction action)
    {
        return ScheduleCrudReducer.OnAddScheduleSuccess(state, action);
    }

    /// <summary>
    /// Pure reducer: Handle RemoveScheduleSuccessAction (no mapping, no side effects).
    /// Following Fluxor best practices: Effects handle transformation, Reducers are pure.
    /// </summary>
    [ReducerMethod]
    public static ApplicationState OnRemoveScheduleSuccess(ApplicationState state, RemoveScheduleSuccessAction action)
    {
        return ScheduleCrudReducer.OnRemoveScheduleSuccess(state, action);
    }

    /// <summary>
    /// Pure reducer: Handle UpdateScheduleSuccessAction with DTO (no mapping, no side effects).
    /// Following Fluxor best practices: Effects handle transformation, Reducers are pure.
    /// </summary>
    [ReducerMethod]
    public static ApplicationState OnUpdateScheduleSuccess(ApplicationState state, UpdateScheduleSuccessAction action)
    {
        return ScheduleCrudReducer.OnUpdateScheduleSuccess(state, action);
    }

    [ReducerMethod]
    public static ApplicationState OnViewSchedule(ApplicationState state, ViewScheduleAction action)
    {
        // Create CurrentBibleReadingSchedule from schedule's Bible reading properties
        BibleReadingStateItem? currentBibleReadingSchedule = null;
        if (action.SelectedSchedule != null &&
            ScheduleStateSyncHelper.HasValidBibleReadingProperties(action.SelectedSchedule))
        {
            currentBibleReadingSchedule = new BibleReadingStateItem
            {
                Id = action.SelectedSchedule.BibleReadingScheduleId ?? 0,
                LanguageCode = action.SelectedSchedule.BibleReadingLanguageCode ?? string.Empty,
                PublicationCode = action.SelectedSchedule.BibleReadingPublicationCode ?? string.Empty,
                SectionNumber = action.SelectedSchedule.BibleReadingSectionNumber,
                ChapterNumber = action.SelectedSchedule.BibleReadingChapterNumber ?? 1,
                FinishedDuration = action.SelectedSchedule.BibleReadingFinishedDuration ?? TimeSpan.Zero,
                AlarmScheduleId = action.SelectedSchedule.Id,
                TranslationName = action.SelectedSchedule.BibleReadingPublicationName ?? string.Empty
            };
        }

        // Deep clone the selected schedule to ensure CurrentSchedule is independent from the item in Schedules collection
        ScheduleStateItem? clonedCurrentSchedule = action.SelectedSchedule?.DeepClone();

        // Sync CurrentMusic from the schedule's music properties (especially important for new schedules)
        // This ensures CurrentMusic matches CurrentSchedule's music type (defaults to Melodies for new schedules)
        MusicStateItem? syncedCurrentMusic = null;
        if (clonedCurrentSchedule != null && ScheduleStateSyncHelper.HasValidMusicProperties(clonedCurrentSchedule))
        {
            syncedCurrentMusic = ScheduleStateSyncHelper.CreateMusicFromCurrent(clonedCurrentSchedule);
        }

        // Always set overlay to visible when navigating to schedule page
        // This ensures the overlay is shown even if it was left visible from a previous visit
        // The overlay will be hidden once containers signal ready
        var overlayVisible = true;

        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: clonedCurrentSchedule,
            currentMusic: syncedCurrentMusic,
            currentBibleReadingSchedule: currentBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: overlayVisible,
            containerReadiness: Models.ContainerReadiness.NotReady,
            pendingScheduleLoad: null); // Clear pending load now that schedule is loaded
    }

    [ReducerMethod]
    public static ApplicationState OnViewExistingSchedule(ApplicationState state, ViewExistingScheduleAction action)
    {
        // Set pending schedule load - ScheduleStateManager will load from DB on background thread
        // Don't set CurrentSchedule yet - it will be set after DB load completes
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: null, // Will be set after DB load
            currentMusic: null,
            currentBibleReadingSchedule: null,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: true, // Show overlay while loading
            containerReadiness: Models.ContainerReadiness.NotReady,
            pendingScheduleLoad: new PendingScheduleLoad(action.ScheduleId, action.IsEnabled));
    }

    [ReducerMethod]
    public static ApplicationState OnBack(ApplicationState state, BackAction action)
    {
        action.CurrentViewModel?.Dispose();
        // Clear schedule-related state when navigating back (same as ResetScheduleStateAction)
        // This ensures state is clean when leaving the schedule page
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: null,
            currentMusic: null,
            currentBibleReadingSchedule: null,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: false, // Reset overlay visibility when leaving schedule page
            containerReadiness: Models.ContainerReadiness.NotReady);
    }

    [ReducerMethod]
    public static ApplicationState OnMusicSelection(ApplicationState state, MusicSelectionAction action)
    {
        // Only update CurrentMusic if it doesn't conflict with CurrentSchedule's music type
        // For new schedules (Id <= 0), CurrentSchedule is the source of truth
        // Don't let MusicSelectionAction override CurrentSchedule's music type
        MusicStateItem? finalCurrentMusic = action.CurrentMusic;
        if (state.CurrentSchedule != null && state.CurrentSchedule.Id <= 0)
        {
            // For new schedules, sync CurrentMusic from CurrentSchedule to ensure consistency
            if (ScheduleStateSyncHelper.HasValidMusicProperties(state.CurrentSchedule))
            {
                finalCurrentMusic = ScheduleStateSyncHelper.CreateMusicFromCurrent(state.CurrentSchedule);
            }
        }

        return StateFactory.CreateUpdatedState(
            state,
            state.CurrentSchedule,
            finalCurrentMusic,
            state.CurrentBibleReadingSchedule);
    }

    [ReducerMethod]
    public static ApplicationState OnSongPublicationSelection(ApplicationState state, SongPublicationSelectionAction action)
    {
        return StateFactory.CreateUpdatedState(
            state,
            state.CurrentSchedule,
            action.CurrentMusic,
            state.CurrentBibleReadingSchedule);
    }

    [ReducerMethod]
    public static ApplicationState OnTrackSelection(ApplicationState state, TrackSelectionAction action)
    {
        return StateFactory.CreateUpdatedState(
            state,
            state.CurrentSchedule,
            action.CurrentMusic,
            state.CurrentBibleReadingSchedule);
    }

    [ReducerMethod]
    public static ApplicationState OnTrackSelected(ApplicationState state, TrackSelectedAction action)
    {
        return StateFactory.CreateUpdatedState(
            state,
            state.CurrentSchedule,
            action.CurrentMusic,
            state.CurrentBibleReadingSchedule);
    }

    [ReducerMethod]
    public static ApplicationState OnBibleSelection(ApplicationState state, BiblePublicationSelectionAction action)
    {
        return StateFactory.CreateUpdatedState(
            state,
            state.CurrentSchedule,
            state.CurrentMusic,
            action.CurrentBibleReadingSchedule);
    }

    [ReducerMethod]
    public static ApplicationState OnSectionSelection(ApplicationState state, SectionSelectionAction action)
    {
        return StateFactory.CreateUpdatedState(
            state,
            state.CurrentSchedule,
            state.CurrentMusic,
            action.CurrentBibleReadingSchedule);
    }

    [ReducerMethod]
    public static ApplicationState OnChapterSelection(ApplicationState state, ChapterSelectionAction action)
    {
        return StateFactory.CreateUpdatedState(
            state,
            state.CurrentSchedule,
            state.CurrentMusic,
            action.CurrentBibleReadingSchedule);
    }

    [ReducerMethod]
    public static ApplicationState OnChapterSelected(ApplicationState state, ChapterSelectedAction action)
    {
        return StateFactory.CreateUpdatedState(
            state,
            state.CurrentSchedule,
            state.CurrentMusic,
            action.CurrentBibleReadingSchedule);
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
        // When showing overlay, reset container readiness to ensure fresh state
        // When hiding overlay, preserve container readiness (it may have been set to ready)
        var containerReadiness = action.IsVisible 
            ? Models.ContainerReadiness.NotReady 
            : state.ContainerReadiness;
        
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: state.CurrentSchedule,
            currentMusic: state.CurrentMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: action.IsVisible,
            containerReadiness: containerReadiness);
    }

    [ReducerMethod]
    public static ApplicationState OnResetScheduleState(ApplicationState state, ResetScheduleStateAction action)
    {
        // Reset all schedule-related state when navigating back to home
        // This ensures only one schedule is in state at any time
        // NOTE: Keep overlay visible to prevent flash during navigation animation
        // The overlay will be hidden when the schedule page is destroyed or when a new schedule page is created
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: null,
            currentMusic: null,
            currentBibleReadingSchedule: null,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible, // Keep current overlay state during navigation
            containerReadiness: Models.ContainerReadiness.NotReady); // Reset container readiness
    }
}
