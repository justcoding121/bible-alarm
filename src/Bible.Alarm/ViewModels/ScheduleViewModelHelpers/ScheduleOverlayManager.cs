#nullable enable
using Bible.Alarm.Stores.Actions;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers;

/// <summary>
/// Handles overlay management for ScheduleViewModel.
/// </summary>
public sealed partial class ScheduleOverlayManager(IDispatcher dispatcher) : IDisposable
{
    /// <summary>
    /// Hides the Home page overlay.
    /// </summary>
    public void HideHomePageOverlay()
    {
        dispatcher.Dispatch(new SetHomePageOverlayAction { IsVisible = false });
    }

    /// <summary>
    /// Shows the Schedule page overlay.
    /// </summary>
    public void ShowSchedulePageOverlay()
    {
        dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = true });
    }

    /// <summary>
    /// Hides the Schedule page overlay.
    /// </summary>
    public void HideSchedulePageOverlay()
    {
        dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
    }

    /// <summary>
    /// Sets the Schedule page overlay visibility.
    /// </summary>
    public void SetSchedulePageOverlay(bool isVisible)
    {
        dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = isVisible });
    }

    /// <summary>
    /// Disposes the overlay manager.
    /// Note: We intentionally do NOT hide the overlay here.
    /// During save/delete navigation, the overlay should stay visible until the page is gone.
    /// The overlay will be reset by the next navigation action or when viewing the home page.
    /// </summary>
    public void Dispose()
    {
        // Do NOT hide overlay here - it causes a flash of content during navigation
        // The overlay state will be reset by ResetScheduleStateAction when navigating to home
    }
}
