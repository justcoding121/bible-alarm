#nullable enable
using Bible;
using Bible.Alarm.Stores.Actions;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers;

/// <summary>
/// Handles overlay management for ScheduleViewModel.
/// </summary>
public sealed class ScheduleOverlayManager(IDispatcher dispatcher)
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
    /// Disposes the overlay manager by hiding overlays.
    /// </summary>
    public void Dispose()
    {
        // Hide overlay when ViewModel is disposed
        HideSchedulePageOverlay();
    }
}
