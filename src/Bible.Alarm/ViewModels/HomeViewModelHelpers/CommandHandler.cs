#nullable enable

using System.Windows.Input;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Schedule;
using CommunityToolkit.Mvvm.Input;
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

    public ICommand CreateAddScheduleCommand(Action<bool>? setIsAddBusy = null, Func<bool>? canExecute = null)
    {
        return new AsyncRelayCommand(async () =>
        {
            // Set IsAddBusy immediately to show loading indicator
            setIsAddBusy?.Invoke(true);
            
            // Wait 50ms to ensure UI thread renders the update before doing backend work
            await Task.Delay(50);

            // Reset all schedule-related state first to ensure clean state
            dispatcher.Dispatch(new ResetScheduleStateAction());

            // Reset container readiness to ensure containers signal ready on new page
            dispatcher.Dispatch(new ResetContainerReadinessAction());

            // Show overlay BEFORE navigation so it's visible immediately when page appears
            dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = true });

            // Navigate immediately - the page will show with busy indicator
            // A fresh Schedule page and ViewModel will be created (both registered as Transient)
            // Containers will be assigned asynchronously once ready (handled by ScheduleViewModel)
            await navigationService.NavigateToScheduleAsync();

            // CurrentSchedule is initialized by ScheduleViewModel after navigation
            
            // Reset IsAddBusy after navigation completes
            setIsAddBusy?.Invoke(false);
        }, canExecute ?? (() => true));
    }

    public ICommand CreateViewScheduleCommand(Action? showProgressBar = null, Func<Task>? hideProgressBar = null)
    {
        // Do NOT use AllowConcurrentExecutions - this prevents double-tap from creating
        // multiple navigations that race with each other and corrupt ScheduleNavigationContext
        return new AsyncRelayCommand<ScheduleListItemViewModel>(async x =>
        {
            if (x == null || x.Schedule == null)
            {
                return;
            }

            // Set IsNavigating immediately to show progress indicator on the item
            x.IsNavigating = true;
            
            // Show progress bar immediately
            showProgressBar?.Invoke();
            
            // Wait 50ms to ensure UI thread renders the update before doing backend work
            await Task.Delay(50);

            if (shouldSkipNavigation(x))
            {
                // Reset IsNavigating and hide progress bar if navigation is skipped
                x.IsNavigating = false;
                if (hideProgressBar != null)
                {
                    await hideProgressBar();
                }
                return;
            }

            await showOverlayAndNavigateAsync(x);
            
            // Reset IsNavigating and hide progress bar after navigation completes
            x.IsNavigating = false;
            if (hideProgressBar != null)
            {
                await hideProgressBar();
            }
        });
    }
}

