#nullable enable

using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Shared.Constants;
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

        var newSchedules = BuildUpdatedSchedulesCollectionIfSaving(state, action.Schedule, action.ShouldSave);

        var updatedCurrentSchedule = ScheduleStateSyncHelper.UpdateCurrentScheduleIfMatches(state, action.Schedule);

        if (newSchedules == null && ReferenceEquals(updatedCurrentSchedule, state.CurrentSchedule))
        {
            Log.Debug(AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnUpdateScheduleFromViewModelCurrentScheduleValuesUnchangedReturningExisting, action.Schedule.Id);
            return state;
        }

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

        return StateFactory.CreateUpdatedState(state, updatedCurrentSchedule);
    }

    private static void LogUpdateStart(UpdateScheduleFromViewModelAction action)
    {
        Log.Information(AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnUpdateScheduleFromViewModelLoggingStart,
            action.Schedule!.Id, action.Schedule.Name,
            action.Schedule.BiblePublicationLanguageCode ?? "null",
            action.Schedule.BiblePublicationLanguageName ?? "null",
            action.Schedule.BiblePublicationCode ?? "null",
            action.Schedule.BiblePublicationName ?? "null");
    }

    private static ObservableHashSet<ScheduleStateItem>? BuildUpdatedSchedulesCollectionIfSaving(
        ApplicationState state,
        ScheduleStateItem scheduleFromAction,
        bool shouldSave)
    {
        if (!shouldSave)
        {
            return null;
        }

        var newSchedules = new ObservableHashSet<ScheduleStateItem>();
        foreach (var scheduleItem in state.Schedules!)
        {
            if (scheduleItem.Id == scheduleFromAction.Id)
            {
                DisplayNamePreservationHelper.PreserveDisplayNamesFromExisting(scheduleFromAction, scheduleItem);
                newSchedules.Add(scheduleFromAction.DeepClone());
            }
            else
            {
                newSchedules.Add(scheduleItem);
            }
        }

        if (!newSchedules.Any(s => s.Id == scheduleFromAction.Id))
        {
            if (scheduleFromAction.Id > 0)
            {
                newSchedules.Add(scheduleFromAction.DeepClone());
            }
            else
            {
                Log.Debug(AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnUpdateScheduleFromViewModelSkippingAddUnsavedScheduleIdZero);
            }
        }

        return newSchedules;
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
        Log.Information(AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnDeleteScheduleReducerCalled,
            action.ScheduleId, action.GetType().FullName);
        return ScheduleCrudReducer.OnDeleteSchedule(state, action);
    }

    /// <summary>
    /// Updates LastPlayedAtUtc for a schedule and rebuilds the Schedules collection so home page re-sorts (recently played first).
    /// </summary>
    [ReducerMethod]
    public static ApplicationState OnUpdateScheduleLastPlayed(ApplicationState state, UpdateScheduleLastPlayedAction action)
    {
        if (state.Schedules == null || state.Schedules.Count == 0)
        {
            return state;
        }

        var updated = false;
        var newSchedules = new ObservableHashSet<ScheduleStateItem>();
        foreach (var item in state.Schedules)
        {
            if (item.Id == action.ScheduleId)
            {
                var clone = item.DeepClone();
                clone.LastPlayedAtUtc = action.LastPlayedAtUtc;
                newSchedules.Add(clone);
                updated = true;
            }
            else
            {
                newSchedules.Add(item);
            }
        }

        if (!updated)
        {
            return state;
        }

        var updatedCurrentSchedule = state.CurrentSchedule;
        if (state.CurrentSchedule?.Id == action.ScheduleId)
        {
            var clone = state.CurrentSchedule.DeepClone();
            clone.LastPlayedAtUtc = action.LastPlayedAtUtc;
            updatedCurrentSchedule = clone;
        }

        return new ApplicationState(
            schedules: newSchedules,
            currentSchedule: updatedCurrentSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible,
            containerReadiness: state.ContainerReadiness,
            pendingScheduleLoad: state.PendingScheduleLoad);
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
        Log.Warning(AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnUpdateScheduleFailure,
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
            // Clear pending load now that schedule is loaded
            pendingScheduleLoad: null);
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
    public static ApplicationState OnCategorySelection(ApplicationState state, CategorySelectionAction action)
    {
        // Do NOT update state here. Category selection triggers async fetch (effect).
        // State is updated only on success via UpdateScheduleFromViewModelAction.
        // On fetch failure, selection must remain unchanged so the UI shows the previous selection.
        return state;
    }

    [ReducerMethod]
    public static ApplicationState OnBibleSelection(ApplicationState state, BiblePublicationSelectionAction action)
    {
        // Bible selection updates CurrentSchedule directly via OnBiblePublicationTrackSelected
        // This reducer is kept for backward compatibility but doesn't need to do anything
        return state;
    }

    [ReducerMethod]
    public static ApplicationState OnBiblePublicationSectionSelection(ApplicationState state, BiblePublicationSectionSelectionAction action)
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

    /// <summary>
    /// Draft-only reducer: updates CurrentSchedule without touching the Schedules collection.
    /// Used during browsing to populate display names on the schedule page
    /// without leaking unsaved changes to the home page list items.
    /// </summary>
    [ReducerMethod]
    public static ApplicationState OnUpdateDraftSchedule(ApplicationState state, UpdateDraftScheduleAction action)
    {
        if (action.Schedule == null || state.CurrentSchedule == null)
        {
            return state;
        }

        if (state.CurrentSchedule.Id != action.Schedule.Id)
        {
            return state;
        }

        var updatedCurrentSchedule = action.Schedule.DeepClone();
        return StateFactory.CreateUpdatedState(state, updatedCurrentSchedule);
    }

    [ReducerMethod]
    public static ApplicationState OnBiblePublicationTrackSelected(ApplicationState state, Actions.BiblePublications.TrackSelectedAction action)
    {
        // IMPORTANT: Update CurrentSchedule synchronously here so the schedule page shows
        // the new selection immediately when modal closes. The async effect runs too late.
        var updatedCurrentSchedule = state.CurrentSchedule;
        if (updatedCurrentSchedule != null && action.CurrentBiblePublicationSchedule != null)
        {
            updatedCurrentSchedule = MergeBiblePublicationTrackSelection(
                updatedCurrentSchedule,
                action.CurrentBiblePublicationSchedule);
        }

        return StateFactory.CreateUpdatedState(state, updatedCurrentSchedule);
    }

    private static ScheduleStateItem MergeBiblePublicationTrackSelection(
        ScheduleStateItem previous,
        BiblePublicationStateItem biblePub)
    {
        var updated = previous.DeepClone();

        var publicationChanged = !string.Equals(previous.BiblePublicationCode, biblePub.PublicationCode, StringComparison.OrdinalIgnoreCase);
        var sectionChanged = !string.Equals(previous.BiblePublicationSectionCode, biblePub.SectionCode, StringComparison.OrdinalIgnoreCase);
        var trackChanged = previous.BiblePublicationTrackCode != biblePub.TrackCode;

        updated.BiblePublicationScheduleId = biblePub.Id > 0 ? biblePub.Id : updated.BiblePublicationScheduleId;

        ApplyLanguageFieldsFromBiblePublicationSelection(updated, biblePub);

        updated.BiblePublicationCode = !string.IsNullOrEmpty(biblePub.PublicationCode)
            ? biblePub.PublicationCode
            : updated.BiblePublicationCode;
        updated.BiblePublicationSectionCode = !string.IsNullOrWhiteSpace(biblePub.SectionCode)
            ? biblePub.SectionCode
            : null;
        updated.BiblePublicationTrackCode = biblePub.TrackCode;

        updated.BiblePublicationName = !string.IsNullOrEmpty(biblePub.PublicationName)
            ? biblePub.PublicationName
            : updated.BiblePublicationName;
        updated.BiblePublicationSectionName = !string.IsNullOrEmpty(biblePub.SectionName)
            ? biblePub.SectionName
            : updated.BiblePublicationSectionName;

        if (!string.IsNullOrEmpty(biblePub.TrackTitle))
        {
            updated.BiblePublicationTrackTitle = biblePub.TrackTitle;
        }
        else if (publicationChanged || sectionChanged || trackChanged)
        {
            updated.BiblePublicationTrackTitle = null;
        }

        ApplyCategoryFieldsFromBiblePublicationSelection(updated, biblePub);

        Log.Debug(AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnBiblePublicationTrackSelectedUpdatedCurrentSchedule,
            updated.BiblePublicationLanguageCode, updated.BiblePublicationCode,
            updated.BiblePublicationSectionCode ?? "null", biblePub.TrackCode, updated.BiblePublicationCategoryName);

        return updated;
    }

    private static void ApplyLanguageFieldsFromBiblePublicationSelection(
        ScheduleStateItem updated,
        BiblePublicationStateItem biblePub)
    {
        var isNoLanguagePublication = string.IsNullOrEmpty(biblePub.LanguageCode);
        var languageChanged = !isNoLanguagePublication &&
                              !string.IsNullOrEmpty(updated.BiblePublicationLanguageCode) &&
                              biblePub.LanguageCode != updated.BiblePublicationLanguageCode;

        if (isNoLanguagePublication || !languageChanged)
        {
            return;
        }

        updated.BiblePublicationLanguageCode = biblePub.LanguageCode;
        updated.BiblePublicationLanguageName = !string.IsNullOrEmpty(biblePub.LanguageName)
            ? biblePub.LanguageName
            : updated.BiblePublicationLanguageName;
        updated.BiblePublicationLanguageDirection = !string.IsNullOrEmpty(biblePub.LanguageDirection)
            ? biblePub.LanguageDirection
            : updated.BiblePublicationLanguageDirection;
    }

    private static void ApplyCategoryFieldsFromBiblePublicationSelection(
        ScheduleStateItem updated,
        BiblePublicationStateItem biblePub)
    {
        if (string.IsNullOrWhiteSpace(updated.BiblePublicationCategoryName))
        {
            if (!string.IsNullOrWhiteSpace(biblePub.CategoryName))
            {
                updated.BiblePublicationCategoryId = biblePub.CategoryId;
                updated.BiblePublicationCategoryName = biblePub.CategoryName;
                Log.Debug(AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnBiblePublicationTrackSelectedSetCategoryFromStateItem,
                    biblePub.CategoryName);
            }
            else if (updated.BiblePublicationCategoryId.HasValue)
            {
                Log.Warning(AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnBiblePublicationTrackSelectedCategoryNameNullCategoryIdExists,
                    updated.BiblePublicationCategoryId.Value);
            }
            else
            {
                Log.Error(AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnBiblePublicationTrackSelectedCategoryNullInBoth);
            }

            return;
        }

        if (!updated.BiblePublicationCategoryId.HasValue && biblePub.CategoryId.HasValue)
        {
            updated.BiblePublicationCategoryId = biblePub.CategoryId;
        }
    }
}
