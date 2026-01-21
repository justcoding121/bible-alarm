#nullable enable

using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.BiblePublications;
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

        // Check if state actually changed to prevent unnecessary state updates and cycles
        // If CurrentSchedule wasn't updated (same reference), return existing state
        if (newSchedules == null && ReferenceEquals(updatedCurrentSchedule, state.CurrentSchedule))
        {
            Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - CurrentSchedule values unchanged, returning existing reference to prevent cycle. ScheduleId: {ScheduleId}", action.Schedule.Id);
            return state;
        }

        // Create new state with new Schedules collection if it was updated, otherwise use existing
        if (newSchedules != null)
        {
            return new ApplicationState(
                schedules: newSchedules,
                currentSchedule: updatedCurrentSchedule,
                isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
                isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible,
                containerReadiness: state.ContainerReadiness,
                pendingScheduleLoad: state.PendingScheduleLoad);
        }
        else
        {
            return StateFactory.CreateUpdatedState(state, updatedCurrentSchedule);
        }
    }

    private static void LogUpdateStart(UpdateScheduleFromViewModelAction action)
    {
        Log.Information("ApplicationReducer: OnUpdateScheduleFromViewModel - ScheduleId: {ScheduleId}, Name: {Name}, LanguageCode: {LanguageCode}, LanguageName: {LanguageName}, PublicationCode: {PublicationCode}, PublicationName: {PublicationName}",
            action.Schedule!.Id, action.Schedule.Name,
            action.Schedule.BiblePublicationLanguageCode ?? "null",
            action.Schedule.BiblePublicationLanguageName ?? "null",
            action.Schedule.BiblePublicationCode ?? "null",
            action.Schedule.BiblePublicationName ?? "null");
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
        // CurrentSchedule is the single source of truth - all data is already in the schedule
        // Deep clone the selected schedule to ensure CurrentSchedule is independent from the item in Schedules collection
        ScheduleStateItem? clonedCurrentSchedule = action.SelectedSchedule?.DeepClone();

        // Always set overlay to visible when navigating to schedule page
        // This ensures the overlay is shown even if it was left visible from a previous visit
        // The overlay will be hidden once containers signal ready
        var overlayVisible = true;

        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: clonedCurrentSchedule,
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
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: false, // Reset overlay visibility when leaving schedule page
            containerReadiness: Models.ContainerReadiness.NotReady);
    }

    [ReducerMethod]
    public static ApplicationState OnMusicSelection(ApplicationState state, MusicSelectionAction action)
    {
        // Music selection updates CurrentSchedule directly via OnMusicTrackSelected
        // This reducer is kept for backward compatibility but doesn't need to do anything
        return state;
    }

    [ReducerMethod]
    public static ApplicationState OnSongPublicationSelection(ApplicationState state, SongPublicationSelectionAction action)
    {
        // Song publication selection updates CurrentSchedule directly via OnMusicTrackSelected
        // This reducer is kept for backward compatibility but doesn't need to do anything
        return state;
    }

    [ReducerMethod]
    public static ApplicationState OnMusicTrackSelection(ApplicationState state, Actions.Music.TrackSelectionAction action)
    {
        // Track selection updates CurrentSchedule directly via OnMusicTrackSelected
        // This reducer is kept for backward compatibility but doesn't need to do anything
        return state;
    }

    [ReducerMethod]
    public static ApplicationState OnMusicTrackSelected(ApplicationState state, Actions.Music.TrackSelectedAction action)
    {
        // IMPORTANT: Update CurrentSchedule synchronously here to ensure schedule page shows
        // the new track immediately when modal closes. The async effect runs too late.
        var updatedCurrentSchedule = state.CurrentSchedule;
        if (updatedCurrentSchedule != null && action.CurrentMusic != null)
        {
            var music = action.CurrentMusic;
            updatedCurrentSchedule = updatedCurrentSchedule.DeepClone();
            updatedCurrentSchedule.MusicType = music.MusicType;
            updatedCurrentSchedule.MusicLanguageCode = music.LanguageCode;
            updatedCurrentSchedule.MusicPublicationCode = music.PublicationCode;
            updatedCurrentSchedule.MusicTrackNumber = music.TrackNumber;
            updatedCurrentSchedule.MusicRepeat = music.Repeat;
            // Also update display names and language direction
            updatedCurrentSchedule.MusicLanguageName = music.LanguageName;
            updatedCurrentSchedule.MusicLanguageDirection = music.LanguageDirection;
            updatedCurrentSchedule.MusicPublicationName = music.PublicationName;
            updatedCurrentSchedule.MusicTrackName = music.TrackName;

            Log.Debug("ApplicationReducer.OnMusicTrackSelected: Updated CurrentSchedule with MusicType={MusicType}, TrackNumber={TrackNumber}, TrackName={TrackName}",
                music.MusicType, music.TrackNumber, music.TrackName);
        }

        return StateFactory.CreateUpdatedState(state, updatedCurrentSchedule);
    }

    [ReducerMethod]
    public static ApplicationState OnMusicSectionSelected(ApplicationState state, Actions.Music.MusicSectionSelectedAction action)
    {
        // IMPORTANT: Update CurrentSchedule synchronously here to ensure schedule page shows
        // the new section immediately when modal closes. The async effect runs too late.
        var updatedCurrentSchedule = state.CurrentSchedule;
        if (updatedCurrentSchedule != null && action.CurrentMusic != null)
        {
            var music = action.CurrentMusic;
            updatedCurrentSchedule = updatedCurrentSchedule.DeepClone();
            updatedCurrentSchedule.MusicType = music.MusicType;
            updatedCurrentSchedule.MusicLanguageCode = music.LanguageCode;
            updatedCurrentSchedule.MusicPublicationCode = music.PublicationCode;
            updatedCurrentSchedule.MusicSectionCode = music.SectionCode;
            updatedCurrentSchedule.MusicTrackNumber = music.TrackNumber;
            updatedCurrentSchedule.MusicRepeat = music.Repeat;
            // Also update display names and language direction
            updatedCurrentSchedule.MusicLanguageName = music.LanguageName;
            updatedCurrentSchedule.MusicLanguageDirection = music.LanguageDirection;
            updatedCurrentSchedule.MusicPublicationName = music.PublicationName;
            updatedCurrentSchedule.MusicSectionName = music.SectionName;
            updatedCurrentSchedule.MusicTrackName = music.TrackName;

            Log.Debug("ApplicationReducer.OnMusicSectionSelected: Updated CurrentSchedule with MusicType={MusicType}, SectionCode={SectionCode}, SectionName={SectionName}, TrackNumber={TrackNumber}",
                music.MusicType, music.SectionCode, music.SectionName, music.TrackNumber);
        }

        return StateFactory.CreateUpdatedState(state, updatedCurrentSchedule);
    }

    [ReducerMethod]
    public static ApplicationState OnCategorySelection(ApplicationState state, CategorySelectionAction action)
    {
        // Category selection updates CurrentSchedule
        var currentSchedule = state.CurrentSchedule;
        if (currentSchedule == null)
        {
            return state;
        }

        var updatedSchedule = currentSchedule.DeepClone();
        updatedSchedule.BiblePublicationCategoryId = action.CategoryId;
        updatedSchedule.BiblePublicationCategoryName = action.CategoryName;
        
        // Cascade: Clear language, publication, section, and track when category changes
        updatedSchedule.BiblePublicationLanguageCode = null;
        updatedSchedule.BiblePublicationLanguageName = null;
        updatedSchedule.BiblePublicationLanguageDirection = null;
        updatedSchedule.BiblePublicationCode = null;
        updatedSchedule.BiblePublicationName = null;
        updatedSchedule.BiblePublicationSectionNumber = null;
        updatedSchedule.BiblePublicationSectionName = null;
        updatedSchedule.BiblePublicationTrackNumber = null;
        updatedSchedule.BiblePublicationTrackTitle = null;
        updatedSchedule.BiblePublicationFinishedDuration = TimeSpan.Zero;

        return StateFactory.CreateUpdatedState(state, updatedSchedule);
    }

    [ReducerMethod]
    public static ApplicationState OnBibleSelection(ApplicationState state, BiblePublicationSelectionAction action)
    {
        // Bible selection updates CurrentSchedule directly via OnBiblePublicationTrackSelected
        // This reducer is kept for backward compatibility but doesn't need to do anything
        return state;
    }

    [ReducerMethod]
    public static ApplicationState OnSectionSelection(ApplicationState state, SectionSelectionAction action)
    {
        // Section selection updates CurrentSchedule directly via OnBiblePublicationTrackSelected
        // This reducer is kept for backward compatibility but doesn't need to do anything
        return state;
    }

    [ReducerMethod]
    public static ApplicationState OnBiblePublicationTrackSelection(ApplicationState state, Actions.BiblePublications.TrackSelectionAction action)
    {
        // Track selection updates CurrentSchedule directly via OnBiblePublicationTrackSelected
        // This reducer is kept for backward compatibility but doesn't need to do anything
        return state;
    }

    [ReducerMethod]
    public static ApplicationState OnBiblePublicationTrackSelected(ApplicationState state, Actions.BiblePublications.TrackSelectedAction action)
    {
        // IMPORTANT: Update CurrentSchedule synchronously here to ensure schedule page shows
        // the new selection immediately when modal closes. The async effect runs too late.
        // Must update ALL bible publication fields to prevent ViewModels from reading stale state
        // and dispatching actions that revert the user's selection (causing state cycles).
        var updatedCurrentSchedule = state.CurrentSchedule;
        if (updatedCurrentSchedule != null && action.CurrentBiblePublicationSchedule != null)
        {
            var biblePub = action.CurrentBiblePublicationSchedule;
            updatedCurrentSchedule = updatedCurrentSchedule.DeepClone();
            
            // Update ALL bible publication fields to ensure CurrentSchedule is fully in sync
            // This prevents ViewModels from reading stale values when they dispatch updates
            updatedCurrentSchedule.BiblePublicationScheduleId = biblePub.Id > 0 ? biblePub.Id : updatedCurrentSchedule.BiblePublicationScheduleId;
            updatedCurrentSchedule.BiblePublicationLanguageCode = !string.IsNullOrEmpty(biblePub.LanguageCode) 
                ? biblePub.LanguageCode 
                : updatedCurrentSchedule.BiblePublicationLanguageCode;
            updatedCurrentSchedule.BiblePublicationCode = !string.IsNullOrEmpty(biblePub.PublicationCode) 
                ? biblePub.PublicationCode 
                : updatedCurrentSchedule.BiblePublicationCode;
            updatedCurrentSchedule.BiblePublicationSectionNumber = biblePub.SectionNumber;
            updatedCurrentSchedule.BiblePublicationTrackNumber = biblePub.TrackNumber;
            // Reset progress to 0.0 when track is changed (by cascade or direct selection)
            updatedCurrentSchedule.BiblePublicationFinishedDuration = TimeSpan.Zero;
            
            // Update display names - use action values if provided, otherwise keep existing
            updatedCurrentSchedule.BiblePublicationLanguageName = !string.IsNullOrEmpty(biblePub.LanguageName) 
                ? biblePub.LanguageName 
                : updatedCurrentSchedule.BiblePublicationLanguageName;
            updatedCurrentSchedule.BiblePublicationLanguageDirection = !string.IsNullOrEmpty(biblePub.LanguageDirection) 
                ? biblePub.LanguageDirection 
                : updatedCurrentSchedule.BiblePublicationLanguageDirection;
            updatedCurrentSchedule.BiblePublicationName = !string.IsNullOrEmpty(biblePub.PublicationName) 
                ? biblePub.PublicationName 
                : updatedCurrentSchedule.BiblePublicationName;
            updatedCurrentSchedule.BiblePublicationSectionName = !string.IsNullOrEmpty(biblePub.SectionName) 
                ? biblePub.SectionName 
                : updatedCurrentSchedule.BiblePublicationSectionName;
            updatedCurrentSchedule.BiblePublicationTrackTitle = !string.IsNullOrEmpty(biblePub.TrackTitle) 
                ? biblePub.TrackTitle 
                : updatedCurrentSchedule.BiblePublicationTrackTitle;

            Log.Debug("ApplicationReducer.OnBiblePublicationTrackSelected: Updated CurrentSchedule with " +
                "LanguageCode={LanguageCode}, PublicationCode={PublicationCode}, SectionNumber={SectionNumber}, TrackNumber={TrackNumber}",
                updatedCurrentSchedule.BiblePublicationLanguageCode, updatedCurrentSchedule.BiblePublicationCode,
                biblePub.SectionNumber, biblePub.TrackNumber);
        }

        return StateFactory.CreateUpdatedState(state, updatedCurrentSchedule);
    }

    [ReducerMethod]
    public static ApplicationState OnSetHomePageOverlay(ApplicationState state, SetHomePageOverlayAction action)
    {
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: state.CurrentSchedule,
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
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible, // Keep current overlay state during navigation
            containerReadiness: Models.ContainerReadiness.NotReady); // Reset container readiness
    }
}
