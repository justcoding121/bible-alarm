#nullable enable

using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers;

internal sealed class ScheduleOverlayTimeoutController : IDisposable
{
    /// <summary>
    /// Hard timeout in milliseconds for the busy overlay.
    /// After this time, the overlay will be hidden regardless of container readiness.
    /// </summary>
    // 10 seconds
    private const int OverlayHardTimeoutMs = 10000;

    private readonly ILogger logger;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;

    private CancellationTokenSource? overlayTimeoutCancellation;
    private bool isContentLoaded;
    private bool isSaving;
    private bool? lastOverlayVisibility;

    public ScheduleOverlayTimeoutController(ILogger logger, IState<ApplicationState> state, IDispatcher dispatcher)
    {
        this.logger = logger;
        this.state = state;
        this.dispatcher = dispatcher;
    }

    public void ResetContentLoaded()
    {
        isContentLoaded = false;
        CancelOverlayTimeout();
    }

    public void OnContentLoaded()
    {
        isContentLoaded = true;

        // Don't hide overlay if we're in the middle of a save operation
        if (isSaving)
        {
            return;
        }

        var stateValue = state.Value;

        // Check if we should hide overlay: both containers ready AND content loaded
        if (stateValue.ContainerReadiness.AllReady && stateValue.IsSchedulePageOverlayVisible)
        {
            CancelOverlayTimeout();
            dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
        }
        else if (stateValue.IsSchedulePageOverlayVisible && overlayTimeoutCancellation == null)
        {
            // Start timeout ONLY if one isn't already running - prevents timeout reset on property changes
            StartOverlayTimeout();
        }
    }

    public void HandleStateChanged(ApplicationState stateValue)
    {
        var currentOverlayVisibility = stateValue.IsSchedulePageOverlayVisible;
        
        // Only process if overlay visibility actually changed or if we need to hide it based on conditions
        // This prevents infinite loops from redundant state changes
        var overlayVisibilityChanged = lastOverlayVisibility != currentOverlayVisibility;
        var wasVisible = lastOverlayVisibility == true;
        lastOverlayVisibility = currentOverlayVisibility;
        
        // Only process overlay-related logic if visibility changed or if we need to check conditions
        // Skip processing if visibility hasn't changed and we're not in a state that requires action
        if (!overlayVisibilityChanged && !currentOverlayVisibility)
        {
            // Overlay is hidden and hasn't changed - nothing to do
            return;
        }
        
        // CRITICAL: Never hide overlay during save operations, even if containers are ready
        // This ensures the overlay stays visible when saving existing schedules
        if (isSaving && currentOverlayVisibility)
        {
            logger.Debug("ScheduleOverlayTimeoutController: Overlay visible during save operation - keeping visible regardless of container readiness");
            CancelOverlayTimeout();
            return;
        }
        
        // Handle overlay visibility based on container readiness and content load state
        if (currentOverlayVisibility && !isSaving)
        {
            if (stateValue.ContainerReadiness.AllReady && isContentLoaded)
            {
                // Both containers ready and content loaded - hide overlay
                // Only dispatch if overlay is currently visible to prevent redundant state changes
                CancelOverlayTimeout();
                // Double-check current state to ensure it's still visible before dispatching
                // This prevents race conditions where state might have changed between checks
                var latestState = state.Value;
                if (latestState.IsSchedulePageOverlayVisible)
                {
                    dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
                }
            }
            else if (isContentLoaded && !stateValue.ContainerReadiness.AllReady && overlayTimeoutCancellation == null)
            {
                // Content loaded but containers not ready - start timeout ONLY if not already running
                StartOverlayTimeout();
            }
        }
        else if (!currentOverlayVisibility && wasVisible)
        {
            // Overlay just became hidden - cancel any running timeout
            CancelOverlayTimeout();
        }
    }

    public void SetIsSaving(bool saving)
    {
        isSaving = saving;
        if (saving)
        {
            // If we start saving, cancel any overlay timeout to avoid unexpected hide mid-save
            CancelOverlayTimeout();
        }
    }

    /// <summary>
    /// Starts a timeout task that will hide the overlay after 10 seconds if containers haven't signaled ready.
    /// This prevents the spinner from spinning forever if a container fails to signal ready.
    /// </summary>
    private void StartOverlayTimeout()
    {
        // Cancel any existing timeout
        CancelOverlayTimeout();

        overlayTimeoutCancellation = new CancellationTokenSource();
        var token = overlayTimeoutCancellation.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(OverlayHardTimeoutMs, token);

                if (!token.IsCancellationRequested)
                {
                    var stateValue = state.Value;
                    if (stateValue.IsSchedulePageOverlayVisible && !stateValue.ContainerReadiness.AllReady)
                    {
                        logger.Warning("ScheduleViewModel: Overlay timeout - containers didn't signal ready within {TimeoutMs}ms, hiding overlay anyway", OverlayHardTimeoutMs);
                        dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Timeout was cancelled, which is expected if containers signaled ready
            }
        }, token);
    }

    private void CancelOverlayTimeout()
    {
        if (overlayTimeoutCancellation != null)
        {
            overlayTimeoutCancellation.Cancel();
            overlayTimeoutCancellation.Dispose();
            overlayTimeoutCancellation = null;
        }
    }

    public void Dispose()
    {
        CancelOverlayTimeout();
    }
}

