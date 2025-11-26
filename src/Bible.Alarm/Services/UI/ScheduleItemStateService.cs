#nullable enable
using Bible.Alarm.ViewModels;
using Bible.Alarm.Views;
using Microsoft.Maui.Controls;
using Serilog;

namespace Bible.Alarm.Services.UI;

public class ScheduleItemStateService
{
    private readonly ILogger _logger;

    public ScheduleItemStateService(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Sets IsBusy to false for the schedule item with the given ID.
    /// This is called after the alarm modal is shown to hide the busy indicator.
    /// </summary>
    public void SetScheduleItemBusyToFalse(int? scheduleId)
    {
        if (scheduleId == null || scheduleId <= 0) return;

        try
        {
            // Try to get the current page and find HomeViewModel
            var mainPage = Application.Current?.MainPage;
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
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Could not set IsBusy to false for schedule {ScheduleId}", scheduleId);
        }
    }
}

