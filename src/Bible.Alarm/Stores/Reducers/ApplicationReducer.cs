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
        updatedSchedule.BiblePublicationSectionCode = null;
        updatedSchedule.BiblePublicationSectionName = null;
        updatedSchedule.BiblePublicationTrackCode = null;
        updatedSchedule.BiblePublicationTrackTitle = null;
        // Do NOT reset progress here. Progress reset is applied only on Save.

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
            var previous = updatedCurrentSchedule;
            updatedCurrentSchedule = updatedCurrentSchedule.DeepClone();
            
            var publicationChanged = !string.Equals(previous.BiblePublicationCode, biblePub.PublicationCode, StringComparison.OrdinalIgnoreCase);
            var sectionChanged = !string.Equals(previous.BiblePublicationSectionCode, biblePub.SectionCode, StringComparison.OrdinalIgnoreCase);
            var trackChanged = previous.BiblePublicationTrackCode != biblePub.TrackCode;

            // Update ALL bible publication fields to ensure CurrentSchedule is fully in sync
            // This prevents ViewModels from reading stale values when they dispatch updates
            updatedCurrentSchedule.BiblePublicationScheduleId = biblePub.Id > 0 ? biblePub.Id : updatedCurrentSchedule.BiblePublicationScheduleId;
            
            // Language code can be changed by:
            // 1. Category change (will default language to E)
            // 2. User explicitly changing the language
            // 3. Selecting a no-language publication (resets to E for consistency)
            // 
            // When a no-language publication is selected, reset language to "E" (English default).
            // This ensures the schedule's language is consistent with what the publication modal will show.
            var isNoLanguagePublication = string.IsNullOrEmpty(biblePub.LanguageCode);
            var languageChanged = !isNoLanguagePublication && 
                                  !string.IsNullOrEmpty(updatedCurrentSchedule.BiblePublicationLanguageCode) &&
                                  biblePub.LanguageCode != updatedCurrentSchedule.BiblePublicationLanguageCode;
            
            if (isNoLanguagePublication)
            {
                // No-language publication selected - reset language to English default.
                // This ensures cascade consistency: the language row shows the default language and publications modal
                // will show English publications + non-languaged publications.
                updatedCurrentSchedule.BiblePublicationLanguageCode = AppConstants.Media.DefaultLanguageCode;
                updatedCurrentSchedule.BiblePublicationLanguageName = null;
                updatedCurrentSchedule.BiblePublicationLanguageDirection = "ltr";
            }
            else if (languageChanged)
            {
                // Language changed - update language code and display names
                // This happens when: category change or user explicitly changed the language
                updatedCurrentSchedule.BiblePublicationLanguageCode = biblePub.LanguageCode;
                updatedCurrentSchedule.BiblePublicationLanguageName = !string.IsNullOrEmpty(biblePub.LanguageName) 
                    ? biblePub.LanguageName 
                    : updatedCurrentSchedule.BiblePublicationLanguageName;
                updatedCurrentSchedule.BiblePublicationLanguageDirection = !string.IsNullOrEmpty(biblePub.LanguageDirection) 
                    ? biblePub.LanguageDirection 
                    : updatedCurrentSchedule.BiblePublicationLanguageDirection;
            }
            // else: Language did not change - preserve language (code, name, direction)
            
            updatedCurrentSchedule.BiblePublicationCode = !string.IsNullOrEmpty(biblePub.PublicationCode) 
                ? biblePub.PublicationCode 
                : updatedCurrentSchedule.BiblePublicationCode;
            updatedCurrentSchedule.BiblePublicationSectionCode = !string.IsNullOrWhiteSpace(biblePub.SectionCode)
                ? biblePub.SectionCode
                : null;
            updatedCurrentSchedule.BiblePublicationTrackCode = biblePub.TrackCode;
            // Do NOT reset progress here. Progress reset is applied only on Save.
            
            // Update other display names - use action values if provided, otherwise keep existing
            updatedCurrentSchedule.BiblePublicationName = !string.IsNullOrEmpty(biblePub.PublicationName) 
                ? biblePub.PublicationName 
                : updatedCurrentSchedule.BiblePublicationName;
            updatedCurrentSchedule.BiblePublicationSectionName = !string.IsNullOrEmpty(biblePub.SectionName) 
                ? biblePub.SectionName 
                : updatedCurrentSchedule.BiblePublicationSectionName;
            // If the selection changed but the action didn't provide a title, clear it to avoid stale titles
            // (e.g. switching from Bible → Music, or between sections/tracks).
            if (!string.IsNullOrEmpty(biblePub.TrackTitle))
            {
                updatedCurrentSchedule.BiblePublicationTrackTitle = biblePub.TrackTitle;
            }
            else if (publicationChanged || sectionChanged || trackChanged)
            {
                updatedCurrentSchedule.BiblePublicationTrackTitle = null;
            }
            
            // ALWAYS preserve category - category can only be changed via CategorySelectionAction
            // DeepClone() already preserves the category, but explicitly ensure it's never null/empty
            // EXCEPTION: If category is null in current schedule but BiblePublicationStateItem has one, use it
            // This handles cases where DispatchDefaultPublicationAsync is called and the category needs to be set
            if (string.IsNullOrWhiteSpace(updatedCurrentSchedule.BiblePublicationCategoryName))
            {
                // Category is null - try to get it from BiblePublicationStateItem
                if (!string.IsNullOrWhiteSpace(biblePub.CategoryName))
                {
                    updatedCurrentSchedule.BiblePublicationCategoryId = biblePub.CategoryId;
                    updatedCurrentSchedule.BiblePublicationCategoryName = biblePub.CategoryName;
                    Log.Debug("ApplicationReducer.OnBiblePublicationTrackSelected: Set category={CategoryName} from BiblePublicationStateItem (was null)",
                        biblePub.CategoryName);
                }
                else
                {
                    // Category is null in both - this should not happen, but preserve what we can
                    // Try to preserve category ID if it exists
                    if (updatedCurrentSchedule.BiblePublicationCategoryId.HasValue)
                    {
                        Log.Warning("ApplicationReducer.OnBiblePublicationTrackSelected: CategoryName is null but CategoryId={CategoryId} exists. Category should always be set.",
                            updatedCurrentSchedule.BiblePublicationCategoryId.Value);
                    }
                    else
                    {
                        Log.Error("ApplicationReducer.OnBiblePublicationTrackSelected: Category is null in both current schedule and BiblePublicationStateItem. Category must always be selected.");
                    }
                }
            }
            else
            {
                // Category exists - ALWAYS preserve it (DeepClone already did this, but be explicit)
                // Category can only be changed via CategorySelectionAction
                // Ensure CategoryId is also preserved
                if (!updatedCurrentSchedule.BiblePublicationCategoryId.HasValue && biblePub.CategoryId.HasValue)
                {
                    // CategoryId might be missing even though CategoryName exists - preserve it
                    updatedCurrentSchedule.BiblePublicationCategoryId = biblePub.CategoryId;
                }
            }

            Log.Debug("ApplicationReducer.OnBiblePublicationTrackSelected: Updated CurrentSchedule with " +
                "LanguageCode={LanguageCode}, PublicationCode={PublicationCode}, SectionCode={SectionCode}, TrackCode={TrackCode}, CategoryName={CategoryName}",
                updatedCurrentSchedule.BiblePublicationLanguageCode, updatedCurrentSchedule.BiblePublicationCode,
                updatedCurrentSchedule.BiblePublicationSectionCode ?? "null", biblePub.TrackCode, updatedCurrentSchedule.BiblePublicationCategoryName);
        }

        return StateFactory.CreateUpdatedState(state, updatedCurrentSchedule);
    }
}
