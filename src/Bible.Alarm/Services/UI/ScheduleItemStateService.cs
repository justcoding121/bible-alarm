#nullable enable
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.ViewModels;
using Bible.Alarm.Views;
using Microsoft.Maui.Controls;
using Serilog;

namespace Bible.Alarm.Services.UI;

public class ScheduleItemStateService(ILogger logger) : IScheduleItemStateService
{
    private readonly ILogger _logger = logger;
    private bool _isDisposed;

    /// <summary>
    /// Sets IsBusy to false for the schedule item with the given ID and hides the Home page overlay.
    /// This is called after the alarm modal is shown to hide the busy indicator.
    /// </summary>
    public void SetScheduleItemBusyToFalse(int? scheduleId)
    {
        if (scheduleId == null || scheduleId <= 0) return;

        try
        {
            // Try to get the current page and find HomeViewModel
            var app = Application.Current;
            if (app?.Windows.Count > 0)
            {
                var mainPage = app.Windows[0].Page;
                if (mainPage is NavigationPage navPage)
                {
                    var currentPage = navPage.CurrentPage;
                    if (currentPage is Home homePage && homePage.BindingContext is HomeViewModel homeViewModel)
                    {
                        // Find the schedule item and set IsBusy to false
                        var scheduleItem = homeViewModel.Schedules?.FirstOrDefault(s => s.ScheduleId == scheduleId);
                        if (scheduleItem != null)
                        {
                            scheduleItem.IsBusy = false;
                        }
                        
                        // Note: Home page overlay is now managed via Fluxor state (SetHomePageOverlayAction)
                        // and is hidden by AlarmModalService after the modal is shown
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Could not set IsBusy to false for schedule {ScheduleId}", scheduleId);
        }
    }

    /// <summary>
    /// Hides the Home page overlay. This is called after the Schedule page is fully loaded and displayed.
    /// </summary>
    public void HideHomePageOverlay()
    {
        try
        {
            // Try to get the current page and find HomeViewModel
            var app = Application.Current;
            if (app?.Windows.Count > 0)
            {
                var mainPage = app.Windows[0].Page;
                if (mainPage is NavigationPage navPage)
                {
                    // Access NavigationStack through INavigation interface
                    var navigation = navPage.Navigation;
                    if (navigation != null)
                    {
                        // Search through the navigation stack to find Home page
                        foreach (var page in navigation.NavigationStack)
                        {
                            if (page is Home homePage && homePage.BindingContext is HomeViewModel homeViewModel)
                            {
                                // Hide the Home page overlay
                                homeViewModel.IsBusy = false;
                                break;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Could not hide Home page overlay");
        }
    }
    
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        
        _isDisposed = true;
        
        // No event handlers to unsubscribe, no injected services to dispose (logger is singleton)
    }
}

