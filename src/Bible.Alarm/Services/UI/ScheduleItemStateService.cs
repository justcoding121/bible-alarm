#nullable enable
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.ViewModels;
using Bible.Alarm.Views;
using Serilog;

namespace Bible.Alarm.Services.UI;

public sealed class ScheduleItemStateService(ILogger logger) : IScheduleItemStateService
{

    /// <summary>
    /// Sets IsBusy to false for the schedule item with the given ID and hides the Home page overlay.
    /// This is called after the alarm modal is shown to hide the busy indicator.
    /// </summary>
    public void SetScheduleItemBusyToFalse(int? scheduleId)
    {
        if (scheduleId is null or <= 0)
        {
            return;
        }

        try
        {
            var homeViewModel = GetHomeViewModelFromCurrentPage();
            if (homeViewModel != null)
            {
                SetScheduleItemBusy(homeViewModel, scheduleId.Value);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Could not set IsBusy to false for schedule {ScheduleId}", scheduleId);
        }
    }

    private static HomeViewModel? GetHomeViewModelFromCurrentPage()
    {
        var app = Application.Current;
        if (app == null || app.Windows.Count == 0)
        {
            return null;
        }

        if (app.Windows[0] == null)
        {
            return null;
        }

        var mainPage = app.Windows[0].Page;
        if (mainPage is NavigationPage navPage)
        {
            var currentPage = navPage.CurrentPage;
            if (currentPage is Home homePage && homePage.BindingContext is HomeViewModel homeViewModel)
            {
                return homeViewModel;
            }
        }

        return null;
    }

    private static void SetScheduleItemBusy(HomeViewModel homeViewModel, int scheduleId)
    {
        var scheduleItem = homeViewModel.Schedules?.FirstOrDefault(s => s.ScheduleId == scheduleId);
        if (scheduleItem != null)
        {
            scheduleItem.IsBusy = false;
        }
    }

    /// <summary>
    /// Hides the Home page overlay. This is called after the Schedule page is fully loaded and displayed.
    /// </summary>
    public void HideHomePageOverlay()
    {
        try
        {
            var homeViewModel = GetHomeViewModelFromNavigationStack();
            if (homeViewModel != null)
            {
                homeViewModel.IsBusy = false;
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Could not hide Home page overlay");
        }
    }

    private static HomeViewModel? GetHomeViewModelFromNavigationStack()
    {
        var app = Application.Current;
        if (app == null || app.Windows.Count == 0)
        {
            return null;
        }

        if (app.Windows[0] == null)
        {
            return null;
        }

        var mainPage = app.Windows[0].Page;
        if (mainPage is NavigationPage navPage)
        {
            var navigation = navPage.Navigation;
            if (navigation != null)
            {
                return FindHomeViewModelInStack(navigation);
            }
        }

        return null;
    }

    private static HomeViewModel? FindHomeViewModelInStack(INavigation navigation)
    {
        foreach (var page in navigation.NavigationStack)
        {
            if (page is Home homePage && homePage.BindingContext is HomeViewModel homeViewModel)
            {
                return homeViewModel;
            }
        }
        return null;
    }

}

