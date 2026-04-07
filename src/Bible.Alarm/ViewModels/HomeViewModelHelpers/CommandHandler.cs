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

            showProgressBar?.Invoke();
            x.IsNavigating = true;

            // Give the UI thread ~3 frames to render both the progress bar and the item spinner
            // before InvokeOnMainThreadAsync re-acquires the main thread for navigation.
            // Task.Yield() (one scheduler quantum) is not enough for ActivityIndicator to start
            // its animation visibly — mirrors the Task.Delay(50) used in CreateAddScheduleCommand.
            await Task.Delay(50);

            try
            {
                // Run on the UI thread so overlay, navigation, and Schedule page creation (Syncfusion/WinUI) run on the correct thread.
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    try
                    {
                        if (shouldSkipNavigation(x))
                        {
                            return;
                        }

                        await showOverlayAndNavigateAsync(x);

#if DEBUG
                        var commandEndTime = DateTime.UtcNow;
                        Log.Information("[PERF] ViewScheduleCommand: Navigation completed, total time: {ElapsedMs}ms",
                            (commandEndTime - commandStartTime).TotalMilliseconds);
#endif
                    }
                    catch (OperationCanceledException)
                    {
                        // Cancellation is expected (e.g. user navigated away)
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
            catch (Exception ex)
            {
                logger.Error(ex, "View schedule failed before navigation for ScheduleId={ScheduleId}", x.Schedule?.Id);
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

    public ICommand CreateOpenFocusSettingsCommand(Action onDismissed)
    {
        return new AsyncRelayCommand(async () =>
        {
            try
            {
                var page = Application.Current?.Windows.FirstOrDefault()?.Page;
                if (page == null)
                {
                    return;
                }

                var openSettings = await page.DisplayAlertAsync(
                    "Sleep Mode",
                    "To ensure your alarms work during iOS Sleep mode, allow Bible Alarm in your Focus settings.\n\n" +
                    "Go to:\nSettings → Focus → Sleep → Allowed Notifications\n\n" +
                    "Then add Bible Alarm to the allowed apps list.",
                    "Open Settings",
                    "Got it");

                if (openSettings)
                {
#if IOS
                    await Launcher.OpenAsync(new Uri("app-settings:"));
#endif
                }
                else
                {
                    HomeViewModelFocusWarningHandler.Dismiss();
                    onDismissed();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error showing Focus settings alert");
            }
        });
    }
}

