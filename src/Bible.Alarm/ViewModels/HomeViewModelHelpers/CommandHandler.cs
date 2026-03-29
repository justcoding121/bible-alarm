#nullable enable

using System.Windows.Input;
using Bible.Alarm.Services.Battery.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.ViewModels.General;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.HomeViewModelHelpers;

/// <summary>
/// Handles command creation and execution for HomeViewModel.
/// </summary>
public class CommandHandler
{
    private readonly ILogger logger;
    private readonly IDispatcher dispatcher;
    private readonly INavigationService navigationService;
    private readonly IServiceProvider serviceProvider;
    private readonly Func<ScheduleListItemViewModel, bool> shouldSkipNavigation;
    private readonly Func<ScheduleListItemViewModel, Task> showOverlayAndNavigateAsync;

    public CommandHandler(
        ILogger logger,
        IDispatcher dispatcher,
        INavigationService navigationService,
        IServiceProvider serviceProvider,
        Func<ScheduleListItemViewModel, bool> shouldSkipNavigation,
        Func<ScheduleListItemViewModel, Task> showOverlayAndNavigateAsync)
    {
        this.logger = logger;
        this.dispatcher = dispatcher;
        this.navigationService = navigationService;
        this.serviceProvider = serviceProvider;
        this.shouldSkipNavigation = shouldSkipNavigation;
        this.showOverlayAndNavigateAsync = showOverlayAndNavigateAsync;
    }

    public ICommand CreateAddScheduleCommand(Action<bool>? setIsAddBusy = null, Func<bool>? canExecute = null)
    {
        return new AsyncRelayCommand(async () =>
        {
            setIsAddBusy?.Invoke(true);

            await Task.Delay(50);

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                dispatcher.Dispatch(new ResetScheduleStateAction());
                dispatcher.Dispatch(new ResetContainerReadinessAction());
                dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = true });

                await navigationService.NavigateToScheduleAsync();

                setIsAddBusy?.Invoke(false);
            });
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

#if DEBUG
            var commandStartTime = DateTime.UtcNow;
            Log.Information("[PERF] ViewScheduleCommand: Tap received at {StartTime}, ScheduleId={ScheduleId}",
                commandStartTime, x.Schedule?.Id);
#endif

            // Set IsNavigating immediately to show progress indicator on the item
            x.IsNavigating = true;
            showProgressBar?.Invoke();

            try
            {
                // Wait 50ms to ensure UI thread renders the update before doing backend work
                await Task.Delay(50);

                // Run the rest on the UI thread so overlay, navigation, and Schedule page creation (Syncfusion/WinUI) run on the correct thread.
                // After Task.Delay the continuation can run on a thread-pool thread; WinUI/Release can throw InvalidCastException otherwise.
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    try
                    {
#if DEBUG
                        Log.Information("[PERF] ViewScheduleCommand: After 50ms delay, checking if should skip");
#endif

                        if (shouldSkipNavigation(x))
                        {
                            return;
                        }

#if DEBUG
                        Log.Information("[PERF] ViewScheduleCommand: About to call showOverlayAndNavigateAsync");
#endif

                        await showOverlayAndNavigateAsync(x);

#if DEBUG
                        var commandEndTime = DateTime.UtcNow;
                        Log.Information("[PERF] ViewScheduleCommand: Navigation completed, total time: {ElapsedMs}ms",
                            (commandEndTime - commandStartTime).TotalMilliseconds);
#endif
                    }
                    catch (OperationCanceledException)
                    {
                        // Cancellation is expected (e.g. user navigated away); still reset state in finally
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "View schedule failed for ScheduleId={ScheduleId}", x.Schedule?.Id);
                        dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
                    }
                    finally
                    {
                        x.IsNavigating = false;
                        if (hideProgressBar != null)
                            await hideProgressBar();
                    }
                });
            }
            finally
            {
                // If Task.Delay or InvokeOnMainThreadAsync throws (e.g. app teardown), ensure UI state is cleared on main thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    x.IsNavigating = false;
                    if (hideProgressBar != null)
                        _ = hideProgressBar();
                });
            }
        });
    }

    public ICommand CreateOpenAlarmSettingsCommand()
    {
        return new AsyncRelayCommand(async () =>
        {
            if (DeviceInfo.Platform != DevicePlatform.Android)
            {
                return;
            }
            try
            {
                var batteryService = serviceProvider.GetService<IBatteryOptimizationService>();
                if (batteryService == null)
                {
                    logger.Warning("IBatteryOptimizationService not available");
                    return;
                }
                var batteryViewModel = new BatteryOptimizationViewModel(logger, navigationService, serviceProvider);
                if (batteryService.CanShowOptimizeActivity())
                {
                    batteryViewModel.CanOptimizeBattery = true;
                }
                batteryViewModel.StartPermissionCheckTimer();
                await navigationService.OpenBatteryOptimizationModalAsync(batteryViewModel);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error opening alarm settings modal from floating button");
            }
        });
    }

    public ICommand CreateOpenNotificationPermissionCommand()
    {
        return new AsyncRelayCommand(async () =>
        {
            try
            {
                var notificationViewModel = new NotificationPermissionViewModel(logger, navigationService, serviceProvider);
                notificationViewModel.StartPermissionCheckTimer();
                await navigationService.OpenNotificationPermissionModalAsync(notificationViewModel);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error opening notification permission modal from floating button");
            }
        });
    }
}

