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
        if (action.ShouldSave)
        {
            var existingScheduleItem = state.Schedules.FirstOrDefault(s => s.Id == action.Schedule.Id);
            if (existingScheduleItem != null)
            {
                DisplayNamePreservationHelper.PreserveDisplayNamesFromExisting(action.Schedule, existingScheduleItem);
                UpdateScheduleInCollection(state.Schedules, existingScheduleItem, action.Schedule);
            }
            else
            {
                state.Schedules.Add(action.Schedule.DeepClone());
            }
        }
        else
        {
            Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - ShouldSave=false, only updating CurrentSchedule, not Schedules collection");
        }

        var updatedCurrentSchedule = ScheduleStateSyncHelper.UpdateCurrentScheduleIfMatches(state, action.Schedule);
        var updatedCurrentBibleReadingSchedule = ScheduleStateSyncHelper.SyncBibleReadingScheduleIfNeeded(action, updatedCurrentSchedule);
        var updatedCurrentMusic = ScheduleStateSyncHelper.SyncMusicIfNeeded(action, updatedCurrentSchedule);

        return StateFactory.CreateUpdatedState(state, updatedCurrentSchedule, updatedCurrentMusic, updatedCurrentBibleReadingSchedule);
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

    private static void UpdateScheduleInCollection(ObservableHashSet<ScheduleStateItem> schedules, ScheduleStateItem existingScheduleItem, ScheduleStateItem actionSchedule)
    {
        // Update the existing item in place to avoid Remove/Add sequence that causes Android Auto to remove the item
        // Deep clone the source to ensure independence from CurrentSchedule
        var sourceSchedule = actionSchedule.DeepClone();
        SchedulePropertyCopier.CopyScheduleProperties(existingScheduleItem, sourceSchedule);
    }

    /// <summary>
    /// Optimistic reducer: Handle DeleteScheduleAction - immediately remove from store for fast UI feedback.
    /// Following Fluxor best practices: Optimistic updates for responsive UX.
    /// </summary>
    [ReducerMethod]
    public static ApplicationState OnDeleteSchedule(ApplicationState state, DeleteScheduleAction action)
    {
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
                BookNumber = action.SelectedSchedule.BibleReadingBookNumber!.Value,
                ChapterNumber = action.SelectedSchedule.BibleReadingChapterNumber!.Value,
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

        return StateFactory.CreateUpdatedState(
            state,
            clonedCurrentSchedule,
            syncedCurrentMusic,
            currentBibleReadingSchedule);
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
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
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
            // This prevents stale CurrentMusic from overriding the new schedule's default (Melodies)
            if (ScheduleStateSyncHelper.HasValidMusicProperties(state.CurrentSchedule))
            {
                finalCurrentMusic = ScheduleStateSyncHelper.CreateMusicFromCurrent(state.CurrentSchedule);
                Log.Debug("ApplicationReducer: OnMusicSelection - New schedule detected, syncing CurrentMusic from CurrentSchedule. MusicType: {MusicType}",
                    finalCurrentMusic.MusicType);
            }
        }

        return StateFactory.CreateUpdatedState(
            state,
            state.CurrentSchedule,
            finalCurrentMusic,
            state.CurrentBibleReadingSchedule);
    }

    [ReducerMethod]
    public static ApplicationState OnSongBookSelection(ApplicationState state, SongBookSelectionAction action)
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
    public static ApplicationState OnBibleSelection(ApplicationState state, BibleSelectionAction action)
    {
        return StateFactory.CreateUpdatedState(
            state,
            state.CurrentSchedule,
            state.CurrentMusic,
            action.CurrentBibleReadingSchedule);
    }

    [ReducerMethod]
    public static ApplicationState OnBookSelection(ApplicationState state, BookSelectionAction action)
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
        Serilog.Log.Debug("ApplicationReducer: OnChapterSelected - Updating CurrentBibleReadingSchedule. LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}, BookNumber: {BookNumber}, ChapterNumber: {ChapterNumber}",
            action.CurrentBibleReadingSchedule?.LanguageCode ?? "null",
            action.CurrentBibleReadingSchedule?.PublicationCode ?? "null",
            action.CurrentBibleReadingSchedule?.BookNumber ?? 0,
            action.CurrentBibleReadingSchedule?.ChapterNumber ?? 0);

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
