#nullable enable
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers.Interfaces;
using Bible.Alarm.Stores.Actions.Schedule;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers;

/// <summary>
/// Handles state management for ScheduleViewModel.
/// Simple flow:
/// 1. Navigate to page (spinner shown via state)
/// 2. Create/Load schedule → set CurrentSchedule in state
/// 3. Containers render from state → signal ready
/// 4. All ready → hide spinner
/// </summary>
public sealed class ScheduleStateManager
{
    private readonly ILogger logger;
    private readonly IScheduleInitializationService scheduleInitializationService;
    private readonly IDispatcher dispatcher;

    private bool isInitializing;

    public ScheduleStateManager(
        IScheduleInitializationService scheduleInitializationService,
        IScheduleStateChangeHandler scheduleStateChangeHandler,
        IDispatcher dispatcher,
        ILogger logger)
    {
        this.logger = logger;
        this.scheduleInitializationService = scheduleInitializationService;
        this.dispatcher = dispatcher;
    }

    /// <summary>
    /// Called when ScheduleViewModel is created.
    /// Handles three flows:
    /// 1. PendingScheduleLoad set: View existing schedule - load from DB on background thread
    /// 2. CurrentSchedule set: View flow with pre-loaded data
    /// 3. Neither set: Add flow - create sample schedule
    /// </summary>
    public void InitializeStateHandling(IState<ApplicationState> state, Action setBusy, Action setOverlayVisible)
    {
        setBusy();

        // Show overlay immediately when page is created (via state action)
        dispatcher.Dispatch(new global::Bible.Alarm.Stores.Actions.SetSchedulePageOverlayAction { IsVisible = true });
        setOverlayVisible();

        // Check navigation context first - this is set by NavigateToScheduleAsync(scheduleId, isEnabled)
        var scheduleIdToLoad = Services.UI.ScheduleNavigationContext.ScheduleIdToLoad;
        var isEnabledToLoad = Services.UI.ScheduleNavigationContext.IsEnabledToLoad;

        // Clear context immediately after reading to prevent stale data
        Services.UI.ScheduleNavigationContext.Clear();

        var currentSchedule = state.Value.CurrentSchedule;

        if (scheduleIdToLoad.HasValue)
        {
            // View existing schedule flow: Load from DB on background thread. Pass in-memory schedule so no-language pub language (e.g. MY) is preferred over DB (e.g. E).
            var existingFromState = state.Value.Schedules.FirstOrDefault(s => s.Id == scheduleIdToLoad.Value);
            logger.Debug("ScheduleStateManager: Loading schedule {ScheduleId} from database", scheduleIdToLoad.Value);
            LoadExistingScheduleAsync(scheduleIdToLoad.Value, isEnabledToLoad, existingFromState);
        }
        else if (currentSchedule != null)
        {
            // View flow: CurrentSchedule already set by HomeNavigationHelper
            // Containers will initialize from state and signal ready
            logger.Debug("ScheduleStateManager: CurrentSchedule exists (Id={Id}), containers will initialize from state", currentSchedule.Id);
        }
        else
        {
            // Add flow: Need to create sample schedule
            logger.Debug("ScheduleStateManager: No CurrentSchedule, creating sample schedule for Add flow");
            InitializeNewScheduleAsync();
        }
    }

    /// <summary>
    /// Loads an existing schedule from DB on background thread and dispatches ViewScheduleAction.
    /// </summary>
    private void LoadExistingScheduleAsync(int scheduleId, bool isEnabled, ScheduleStateItem? existingFromState = null)
    {
        if (isInitializing) return;
        isInitializing = true;

        Task.Run(async () =>
        {
            try
            {
                var scheduleStateItem = await scheduleInitializationService.LoadExistingScheduleAsync(scheduleId, isEnabled, existingFromState);

                if (scheduleStateItem == null)
                {
                    logger.Error("ScheduleStateManager: Failed to load schedule {ScheduleId} from database", scheduleId);
                    isInitializing = false;
                    // Hide overlay to prevent infinite spinner - the page will show empty state
                    // which is better than spinning forever
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        dispatcher.Dispatch(new global::Bible.Alarm.Stores.Actions.SetSchedulePageOverlayAction { IsVisible = false });
                    });
                    return;
                }

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    logger.Debug("ScheduleStateManager: Dispatching ViewScheduleAction for loaded schedule {ScheduleId}", scheduleId);
                    dispatcher.Dispatch(new ViewScheduleAction(scheduleStateItem));
                    isInitializing = false;
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error loading existing schedule {ScheduleId}", scheduleId);
                isInitializing = false;
                // Hide overlay on error to prevent infinite spinner
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    dispatcher.Dispatch(new global::Bible.Alarm.Stores.Actions.SetSchedulePageOverlayAction { IsVisible = false });
                });
            }
        });
    }

    /// <summary>
    /// Creates a sample schedule and dispatches ViewScheduleAction.
    /// Containers will initialize when they receive state change with CurrentSchedule set.
    /// </summary>
    private void InitializeNewScheduleAsync()
    {
        if (isInitializing) return;
        isInitializing = true;

        Task.Run(async () =>
        {
            try
            {
                var scheduleStateItem = await scheduleInitializationService.InitializeNewScheduleAsync();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    logger.Debug("ScheduleStateManager: Dispatching ViewScheduleAction for new schedule");
                    dispatcher.Dispatch(new ViewScheduleAction(scheduleStateItem));
                    isInitializing = false;
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error creating sample schedule");
                isInitializing = false;
                // Hide overlay on error to prevent infinite spinner
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    dispatcher.Dispatch(new global::Bible.Alarm.Stores.Actions.SetSchedulePageOverlayAction { IsVisible = false });
                });
            }
        });
    }

    /// <summary>
    /// Called on state changes. Updates overlay visibility and notifies property changes.
    /// The overlay is hidden when ContainerReadiness.AllReady is true (handled in ScheduleViewModel).
    /// </summary>
    public void HandleStateChanged(
        IState<ApplicationState> state,
        Action<bool> setOverlayVisible,
        Action notifySchedulePropertiesChanged,
        Action<object?, EventArgs> onCurrentScheduleChanged)
    {
        var stateValue = state.Value;

        // Sync overlay visibility with state
        setOverlayVisible(stateValue.IsSchedulePageOverlayVisible);

        // Notify UI of property changes
        notifySchedulePropertiesChanged();
    }

    /// <summary>
    /// Unused - kept for interface compatibility. 
    /// Schedule changes are handled via state subscriptions in containers.
    /// </summary>
    public void HandleCurrentScheduleChanged(IState<ApplicationState> state)
    {
        // Containers handle their own initialization from state.
        // This method is no longer needed in the simplified flow.
    }
}
