#nullable enable

using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores.Actions;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
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
            dispatcher.Dispatch(new ResetScheduleStateAction());
            await navigationService.NavigateToScheduleAsync();
            dispatcher.Dispatch(new ViewScheduleAction(null));
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

