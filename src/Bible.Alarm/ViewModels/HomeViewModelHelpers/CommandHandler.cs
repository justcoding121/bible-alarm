#nullable enable

using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Schedule;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.Maui.ApplicationModel;
using System.Windows.Input;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.HomeViewModelHelpers;

/// <summary>
/// Handles command creation and execution for HomeViewModel.
/// </summary>
public class CommandHandler
{
    private readonly IDispatcher dispatcher;
    private readonly INavigationService navigationService;
    private readonly Func<ScheduleListItemViewModel, bool> shouldSkipNavigation;
    private readonly Func<ScheduleListItemViewModel, Task> showOverlayAndNavigateAsync;

    public CommandHandler(
        IDispatcher dispatcher,
        INavigationService navigationService,
        Func<ScheduleListItemViewModel, bool> shouldSkipNavigation,
        Func<ScheduleListItemViewModel, Task> showOverlayAndNavigateAsync)
    {
        this.dispatcher = dispatcher;
        this.navigationService = navigationService;
        this.shouldSkipNavigation = shouldSkipNavigation;
        this.showOverlayAndNavigateAsync = showOverlayAndNavigateAsync;
    }

    public ICommand CreateAddScheduleCommand()
    {
        return new AsyncRelayCommand(async () =>
        {
            // Show overlay BEFORE navigation so it's visible immediately when page appears
            dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = true });
            
            // Reset state
            dispatcher.Dispatch(new ResetScheduleStateAction());
            
            // Navigate immediately - the page will show with busy indicator
            // Containers will be assigned asynchronously once ready (handled by ScheduleViewModel)
            await navigationService.NavigateToScheduleAsync();
            
            // CurrentSchedule is initialized by ScheduleViewModel after navigation
        });
    }

    public ICommand CreateViewScheduleCommand()
    {
        return new AsyncRelayCommand<ScheduleListItemViewModel>(async x =>
        {
            if (x == null || x.Schedule == null)
            {
                return;
            }

            if (shouldSkipNavigation(x))
            {
                return;
            }

            await showOverlayAndNavigateAsync(x);
        });
    }
}

