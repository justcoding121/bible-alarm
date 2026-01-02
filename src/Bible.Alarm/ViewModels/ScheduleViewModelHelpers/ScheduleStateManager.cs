#nullable enable
using Bible;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Schedule;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers;

/// <summary>
/// Handles state management and schedule lifecycle for ScheduleViewModel.
/// </summary>
public sealed class ScheduleStateManager
{
    private readonly ILogger logger;
    private readonly IScheduleInitializationService scheduleInitializationService;
    private readonly ScheduleStateChangeHandler scheduleStateChangeHandler;
    private readonly IDispatcher dispatcher;

    // Tracking fields
    private int lastScheduleId = -1;
    private bool modelInitialized;
    private bool isInitializingNewSchedule;
    private bool isSaving;

    // Track previous music state to detect changes
    private MusicType? lastMusicType;
    private int? lastMusicTrackNumber;
    private string? lastMusicPublicationCode;
    private string? lastMusicLanguageCode;
    private bool? lastMusicRepeat;

    // Track last processed state to prevent redundant processing
    private int lastProcessedScheduleId = -1;
    private bool lastProcessedOverlayVisible = true;

    public ScheduleStateManager(
        IScheduleInitializationService scheduleInitializationService,
        ScheduleStateChangeHandler scheduleStateChangeHandler,
        IDispatcher dispatcher,
        ILogger logger)
    {
        this.logger = logger;
        this.scheduleInitializationService = scheduleInitializationService;
        this.scheduleStateChangeHandler = scheduleStateChangeHandler;
        this.dispatcher = dispatcher;
    }

    public void InitializeStateHandling(IState<ApplicationState> state, Action setBusy, Action setOverlayVisible)
    {
        setBusy();
        modelInitialized = false;
        setOverlayVisible();

        var currentState = state.Value;
        if (currentState.CurrentSchedule != null)
        {
            MainThread.BeginInvokeOnMainThread(() => HandleCurrentScheduleChanged(state));
        }
        else
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                if (state.Value.CurrentSchedule != null && !modelInitialized)
                {
                    await MainThread.InvokeOnMainThreadAsync(() => HandleCurrentScheduleChanged(state));
                }
            });
        }
    }

    public void HandleStateChanged(
        IState<ApplicationState> state,
        Action<bool> setOverlayVisible,
        Action notifySchedulePropertiesChanged,
        Action<object?, EventArgs> onCurrentScheduleChanged)
    {
        var stateValue = state.Value;
        var currentScheduleId = stateValue.CurrentSchedule?.Id ?? -1;
        var newOverlayVisible = stateValue.IsSchedulePageOverlayVisible;
        var isScheduleUpdate = currentScheduleId == lastScheduleId && modelInitialized;

        // Early exit if we've already processed this exact state (only for schedule updates, not initial loads)
        // Also check if the property value itself hasn't changed to prevent unnecessary updates
        if (isScheduleUpdate &&
            currentScheduleId == lastProcessedScheduleId &&
            newOverlayVisible == lastProcessedOverlayVisible &&
            newOverlayVisible == GetCurrentOverlayVisible())
        {
            logger.Debug("ScheduleViewModel: OnStateChanged - Skipping processing as no relevant changes detected. ScheduleId: {ScheduleId}, OverlayVisible: {OverlayVisible}",
                currentScheduleId, newOverlayVisible);
            return;
        }

        // Early exit if property value already matches state value (prevents unnecessary PropertyChanged events)
        // This is especially important when visiting the same schedule multiple times
        // Check this BEFORE checking isScheduleUpdate to catch all cases where property already matches
        if (newOverlayVisible == GetCurrentOverlayVisible())
        {
            // Still update tracking fields to prevent future unnecessary processing
            // Only update if this is a schedule update (not initial load) to avoid interfering with initialization
            if (isScheduleUpdate)
            {
                lastProcessedScheduleId = currentScheduleId;
                lastProcessedOverlayVisible = newOverlayVisible;
            }
            // Always return early if property already matches - no need to process further
            return;
        }

        // Only update property if value actually changed to prevent unnecessary PropertyChanged events
        if (newOverlayVisible != GetCurrentOverlayVisible())
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Double-check the value hasn't changed since we checked (race condition protection)
                if (newOverlayVisible != GetCurrentOverlayVisible())
                {
                    if (!newOverlayVisible || !isScheduleUpdate)
                    {
                        setOverlayVisible(newOverlayVisible);
                    }
                }
            });
        }

        notifySchedulePropertiesChanged();

        if (currentScheduleId != lastScheduleId || !modelInitialized)
        {
            onCurrentScheduleChanged(null, EventArgs.Empty);
        }

        // Update last processed state after handling changes
        lastProcessedScheduleId = currentScheduleId;
        lastProcessedOverlayVisible = newOverlayVisible;
    }

    public void HandleCurrentScheduleChanged(IState<ApplicationState> state)
    {
        var stateValue = state.Value;

        if (stateValue.CurrentSchedule != null)
        {
            HandleExistingScheduleUpdate(stateValue);
        }
        else
        {
            HandleNewScheduleInitialization(stateValue);
        }
    }

    private void HandleExistingScheduleUpdate(ApplicationState stateValue)
    {
        var currentScheduleId = stateValue.CurrentSchedule!.Id;

        if (currentScheduleId == lastScheduleId && modelInitialized)
        {
            if (isSaving) return;
            HandleScheduleUpdateFromState(stateValue, currentScheduleId);
            // Only dispatch if overlay is currently visible to avoid infinite loops
            if (stateValue.IsSchedulePageOverlayVisible)
            {
                dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
            }
            return;
        }

        // Reset tracking fields when schedule ID changes
        lastProcessedScheduleId = -1;
        lastProcessedOverlayVisible = true;

        LoadScheduleFromState(stateValue, currentScheduleId);
    }

    private bool HandleScheduleUpdateFromState(ApplicationState stateValue, int currentScheduleId)
    {
        var currentSchedule = stateValue.CurrentSchedule;
        var hasChanges = scheduleStateChangeHandler.HandleScheduleUpdateFromState(
            currentSchedule,
            ref lastMusicType,
            ref lastMusicTrackNumber,
            ref lastMusicPublicationCode,
            ref lastMusicLanguageCode,
            ref lastMusicRepeat,
            out var musicChanged);

        return hasChanges;
    }

    private void LoadScheduleFromState(ApplicationState stateValue, int currentScheduleId)
    {
        isInitializingNewSchedule = false;
        var currentScheduleItem = stateValue.CurrentSchedule!;

        // Reset tracking fields when loading a new schedule
        lastProcessedScheduleId = -1;
        lastProcessedOverlayVisible = stateValue.IsSchedulePageOverlayVisible;

        _ = Task.Run(async () =>
        {
            try
            {
                var scheduleStateItemSnapshot = currentScheduleItem.DeepClone();
                scheduleInitializationService.InitializeTrackingFields(
                    scheduleStateItemSnapshot,
                    ref lastScheduleId,
                    ref lastMusicType,
                    ref lastMusicTrackNumber,
                    ref lastMusicPublicationCode,
                    ref lastMusicLanguageCode,
                    ref lastMusicRepeat);

                // Handle completion on main thread
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    try
                    {
                        await CompleteScheduleLoadAsync();
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Error in LoadScheduleFromState main thread handler");
                        HandleLoadError();
                    }
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in LoadScheduleFromState data preparation");
                await MainThread.InvokeOnMainThreadAsync(HandleLoadError);
            }
        });
    }

    private async Task CompleteScheduleLoadAsync()
    {
        await scheduleInitializationService.CompleteScheduleLoadAsync();

        // Mark model as initialized for existing schedules
        modelInitialized = true;
    }

    private void HandleLoadError()
    {
        // Error handling logic would be passed in as callbacks
    }

    private void HandleNewScheduleInitialization(ApplicationState stateValue)
    {
        if (modelInitialized && !isSaving)
        {
            ResetViewModelForNewSchedule();
        }
        if (!modelInitialized && !isInitializingNewSchedule)
        {
            InitializeNewSchedule();
        }
    }

    private void ResetViewModelForNewSchedule()
    {
        modelInitialized = false;
        lastScheduleId = -1;
        scheduleStateChangeHandler.ResetMusicTrackingFields(
            ref lastMusicType,
            ref lastMusicTrackNumber,
            ref lastMusicPublicationCode,
            ref lastMusicLanguageCode,
            ref lastMusicRepeat);
    }

    private void InitializeNewSchedule()
    {
        isInitializingNewSchedule = true;

        Task.Run(async () =>
        {
            var scheduleStateItem = await scheduleInitializationService.InitializeNewScheduleAsync();
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var currentState = GetCurrentState();
                if (isInitializingNewSchedule && !modelInitialized && currentState.CurrentSchedule == null)
                {
                    modelInitialized = true;
                    scheduleInitializationService.InitializeTrackingFields(
                        scheduleStateItem,
                        ref lastScheduleId,
                        ref lastMusicType,
                        ref lastMusicTrackNumber,
                        ref lastMusicPublicationCode,
                        ref lastMusicLanguageCode,
                        ref lastMusicRepeat);

                    dispatcher.Dispatch(new ViewScheduleAction(scheduleStateItem));
                }
                else
                {
                    await FinalizeNewScheduleInitialization();
                }
            });
        });
    }

    private async Task FinalizeNewScheduleInitialization()
    {
        isInitializingNewSchedule = false;
    }

    private ApplicationState GetCurrentState()
    {
        // This would need to be passed in or accessed differently
        // For now, returning a placeholder
        return new ApplicationState();
    }

    private bool GetCurrentOverlayVisible()
    {
        // This would need to be passed in or accessed differently
        // For now, returning a placeholder
        return true;
    }

    // Properties and methods to expose state
    public int LastScheduleId => lastScheduleId;
    public bool ModelInitialized => modelInitialized;
    public bool IsInitializingNewSchedule => isInitializingNewSchedule;
    public bool IsSaving => isSaving;

    public void SetIsSaving(bool value) => isSaving = value;
    public void SetModelInitialized(bool value) => modelInitialized = value;
    public void SetLastScheduleId(int value) => lastScheduleId = value;
}
