#nullable enable

using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
#if ANDROID || IOS
using Bible.Alarm.Services.Scheduler.ScheduleStateServiceHelpers;
#endif
using Bible.Alarm.ViewModels.General;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;
using Fluxor;
using Microsoft.Maui.ApplicationModel;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
using Bible.Alarm.Shared.Models.Schedule;

#if ANDROID
using Bible.Alarm.Platforms.Android.Services.Helpers;
#elif IOS
using Bible.Alarm.Platforms.iOS.Services.Helpers;
#endif

namespace Bible.Alarm.Services.Scheduler;

#pragma warning disable CS9113 // Parameters used in platform-specific (#if ANDROID/IOS) code paths only
public sealed class ScheduleStateService(
    ILogger logger,
    IAlarmScheduleService alarmScheduleService,
    IAlarmService alarmService,
    INotificationService notificationService,
    IToastService toastService,
    IDispatcher dispatcher,
    INavigationService navigationService,
    IServiceProvider serviceProvider)
#pragma warning restore CS9113
    : IScheduleStateService, IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool isDisposed;

    public async Task<bool> UpdateScheduleEnabledStateAsync(int scheduleId, bool isEnabled)
    {
#if !ANDROID && !IOS
        if (isEnabled && !await CheckNotificationPermissionsAsync(scheduleId))
        {
            return false;
        }
#endif
        if (isEnabled)
        {
#if ANDROID
            var (androidHandled, androidUpdated) = await ScheduleStateServiceAndroidEnableWithPermission.TryHandleEnableAsync(
                scheduleId, isEnabled, logger, alarmScheduleService, alarmService, dispatcher, navigationService,
                serviceProvider, IsSecurityException, HandleSecurityExceptionAsync, cancellationTokenSource.Token);
            if (androidHandled)
            {
                if (androidUpdated is AlarmSchedule storedAndroidSchedule)
                {
                    UpdateFluxorStore(storedAndroidSchedule);
                    await ShowNotificationIfEnabledAsync(isEnabled, storedAndroidSchedule);
                }

                return true;
            }
#elif IOS
            var (iosHandled, iosUpdated) = await ScheduleStateServiceIosEnableWithPermission.TryHandleEnableAsync(
                scheduleId, isEnabled, logger, alarmScheduleService, alarmService, dispatcher, navigationService,
                serviceProvider, IsSecurityException, HandleSecurityExceptionAsync, UpdateFluxorStore, cancellationTokenSource.Token);
            if (iosHandled)
            {
                if (iosUpdated is AlarmSchedule storedIosSchedule)
                {
                    UpdateFluxorStore(storedIosSchedule);
                    await ShowNotificationIfEnabledAsync(isEnabled, storedIosSchedule);
                }

                return true;
            }
#endif
        }

        // Update database and alarm service
        AlarmSchedule? updatedSchedule = null;
        try
        {
            updatedSchedule = await UpdateScheduleEnabledStateInDatabaseAsync(scheduleId, isEnabled);
        }
        catch (Exception ex)
        {
            if (IsSecurityException(ex))
            {
                return await HandleSecurityExceptionAsync(scheduleId, ex);
            }
            throw;
        }

        UpdateFluxorStore(updatedSchedule);
        await ShowNotificationIfEnabledAsync(isEnabled, updatedSchedule);

        return true;
    }

#pragma warning disable CS9113 // Parameter 'notificationService' is used in iOS/WinUI paths (#else block)
    private async Task<bool> CheckNotificationPermissionsAsync(int scheduleId)
    {
#if ANDROID
        var schedule = await alarmScheduleService.GetScheduleByIdAsync(scheduleId, false, false);
        // Android: Only check notification permission if "Tap to Play" (NotificationEnabled) is enabled
        // The main reminder (IsEnabled) can work without notification permission
        // However, if user enables IsEnabled (reminder) from home page and NotificationEnabled is already true,
        // we need to check/request notification permission because tap-to-play requires it
        if (schedule != null && schedule.NotificationEnabled)
        {
            logger.Information(AppConstants.Logging.ScheduleStateServiceDiagnosticsLog.AndroidScheduleNotificationEnabledCheckingPermissionBeforeReminder, scheduleId);
            // Check permission status without waiting (non-blocking)
            // Permission requests are handled by ViewModels via the modal
            var granted = NotificationPermissionHelper.IsNotificationPermissionGranted();
            if (!granted)
            {
                logger.Warning(AppConstants.Logging.ScheduleEnableDiagnosticsLog.CannotEnableNotificationDeniedTapToPlay, scheduleId);
                return false;
            }
            logger.Information(AppConstants.Logging.ScheduleStateServiceDiagnosticsLog.AndroidNotificationPermissionGrantedForSchedule, scheduleId);
        }
        else if (schedule != null)
        {
            logger.Debug(AppConstants.Logging.ScheduleStateServiceDiagnosticsLog.AndroidScheduleNotificationDisabledNoPermissionCheckNeeded, scheduleId);
        }
        return true; // Android permission check passed or not needed
#elif IOS
        // iOS: Always check notification permission when enabling a reminder
        // iOS always uses notifications for alarms, so permission is required for the reminder itself
        // There is no separate "Tap to Play" toggle on iOS
        // Request permission if not already granted
        var permissionService = IOSNotificationPermissionService.Instance;
        if (!permissionService.IsGranted)
        {
            // Request permission - this will show the iOS permission dialog
            logger.Information(AppConstants.Logging.ScheduleStateServiceDiagnosticsLog.RequestingIosNotificationPermissionForSchedule, scheduleId);
            
            // Use TaskCompletionSource to wait for permission response
            var tcs = new TaskCompletionSource<bool>();
            EventHandler? grantedHandler = null;
            EventHandler? deniedHandler = null;
            
            grantedHandler = (sender, e) =>
            {
                permissionService.PermissionGranted -= grantedHandler;
                permissionService.PermissionDenied -= deniedHandler;
                if (!tcs.Task.IsCompleted)
                {
                    tcs.SetResult(true);
                }
            };
            
            deniedHandler = (sender, e) =>
            {
                permissionService.PermissionGranted -= grantedHandler;
                permissionService.PermissionDenied -= deniedHandler;
                if (!tcs.Task.IsCompleted)
                {
                    tcs.SetResult(false);
                }
            };
            
            permissionService.PermissionGranted += grantedHandler;
            permissionService.PermissionDenied += deniedHandler;
            
            try
            {
                // Request permission
                permissionService.RequestPermissionIfNeeded();
                
                // Wait for user response (with timeout of 10 seconds)
                var timeoutTask = Task.Delay(10000);
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
                
                bool granted = false;
                if (completedTask == tcs.Task)
                {
                    try
                    {
                        granted = await tcs.Task;
                    }
                    catch (Exception)
                    {
                        granted = false;
                    }
                }
                else
                {
                    // Timeout - user didn't respond, assume denied
                    logger.Warning(AppConstants.Logging.ScheduleEnableDiagnosticsLog.PermissionRequestTimeoutForSchedule, scheduleId);
                    granted = false;
                }
                
                if (!granted)
                {
                    logger.Warning(AppConstants.Logging.ScheduleEnableDiagnosticsLog.CannotEnableIosRemindersPermissionDenied, scheduleId);
                    await toastService.ShowMessage(
                        AppConstants.ToastMessages.NotificationPermissionRequiredRemindersIos,
                        7);
                    return false;
                }
            }
            finally
            {
                // Clean up event handlers
                permissionService.PermissionGranted -= grantedHandler;
                permissionService.PermissionDenied -= deniedHandler;
            }
        }
        return true; // iOS permission check passed
#else
        var schedule = await alarmScheduleService.GetScheduleByIdAsync(scheduleId, false, false);
        // WinUI and other platforms
        if (DeviceInfo.Platform == DevicePlatform.WinUI
            && schedule != null && schedule.NotificationEnabled
            && !await notificationService.CanScheduleAsync())
        {
            logger.Warning(AppConstants.Logging.ScheduleEnableDiagnosticsLog.CannotEnableNotificationDeniedTapToPlay, scheduleId);
            await toastService.ShowMessage(
                AppConstants.ToastMessages.NotificationPermissionRequiredTapToPlayWinUi,
                7);
            return false;
        }
        return true;
#endif
    }
#pragma warning restore CS9113

    private async Task<AlarmSchedule> UpdateScheduleEnabledStateInDatabaseAsync(int scheduleId, bool isEnabled)
    {
        var updatedSchedule = await alarmScheduleService.UpdateScheduleByIdAsync(
            scheduleId,
            schedule => schedule.IsEnabled = isEnabled,
            cancellationTokenSource.Token);

        await alarmService.Update(updatedSchedule);
        return updatedSchedule;
    }

    private static bool IsSecurityException(Exception ex)
    {
        for (Exception? cur = ex; cur != null; cur = cur.InnerException)
        {
            var exceptionType = cur.GetType().FullName;
            if (string.Equals(exceptionType, "Java.Lang.SecurityException", StringComparison.Ordinal) ||
                cur.Message.Contains("SCHEDULE_EXACT_ALARM", StringComparison.Ordinal) ||
                cur.Message.Contains("USE_EXACT_ALARM", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> HandleSecurityExceptionAsync(int scheduleId, Exception ex)
    {
        logger.Error(ex, AppConstants.Logging.AndroidExactAlarmSchedulingLog.SecurityExceptionUpdatingSchedule, scheduleId);

        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            await toastService.ShowMessage(
                AppConstants.ToastMessages.CannotScheduleReminderExactAlarmPermission,
                7);
        }

        return false;
    }

    private void UpdateFluxorStore(AlarmSchedule? updatedSchedule)
    {
        if (updatedSchedule != null)
        {
            dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
        }
    }

    private async Task ShowNotificationIfEnabledAsync(bool isEnabled, AlarmSchedule? updatedSchedule)
    {
        if (isEnabled && updatedSchedule != null)
        {
            await toastService.ShowScheduledNotification(updatedSchedule);
        }
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // Cancel and dispose cancellation token source
        try
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
        catch (Exception ex)
        {
            // Ignore errors during cancellation/disposal
            logger.Warning(ex, AppConstants.Logging.DisposableLifetimeLog.ErrorDuringCancellationTokenSourceDisposal);
        }

        // All injected services are singletons, so don't dispose them
        // No event handlers to unsubscribe
    }
}

