#nullable enable

using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Actions.Schedule;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
using Bible.Alarm.Shared.Models.Schedule;

#if ANDROID
using Bible.Alarm.Platforms.Android.Services.Helpers;
#elif IOS
using Bible.Alarm.Platforms.iOS.Services.Helpers;
#endif

namespace Bible.Alarm.Services.Scheduler;

public sealed class ScheduleStateService(
    ILogger logger,
    IAlarmScheduleService alarmScheduleService,
    IAlarmService alarmService,
    INotificationService notificationService,
    IToastService toastService,
    IDispatcher dispatcher)
    : IScheduleStateService, IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly INotificationService notificationService = notificationService;
    private bool isDisposed;

    public async Task<bool> UpdateScheduleEnabledStateAsync(int scheduleId, bool isEnabled)
    {
        // Check notification permissions if enabling
        if (isEnabled)
        {
#if ANDROID
            // Android: If NotificationEnabled is true but permission is denied,
            // disable NotificationEnabled but still allow IsEnabled to be enabled
            // Android reminder can work without notification permission
            var schedule = await alarmScheduleService.GetScheduleByIdAsync(scheduleId, false, false);
            if (schedule != null && schedule.NotificationEnabled)
            {
                var granted = await NotificationPermissionHelper.RequestNotificationPermissionIfNeededAsync();
                if (!granted)
                {
                    logger.Warning("Cannot enable NotificationEnabled for schedule {ScheduleId} - notification permission denied. Disabling NotificationEnabled but allowing reminder to be enabled.", scheduleId);
                    await toastService.ShowMessage(
                        "Notification permission is required for tap-to-play alarms. Tap-to-play has been disabled, but reminder is enabled.",
                        7);
                    
                    // Disable NotificationEnabled but continue to enable IsEnabled
                    AlarmSchedule? updatedSchedule = null;
                    try
                    {
                        updatedSchedule = await alarmScheduleService.UpdateScheduleByIdAsync(
                            scheduleId,
                            s => 
                            {
                                s.IsEnabled = isEnabled;
                                s.NotificationEnabled = false;
                            },
                            cancellationTokenSource.Token);
                    }
                    catch (Exception ex)
                    {
                        if (IsSecurityException(ex))
                        {
                            return await HandleSecurityExceptionAsync(scheduleId, ex);
                        }
                        throw;
                    }
                    
                    await alarmService.Update(updatedSchedule);
                    UpdateFluxorStore(updatedSchedule);
                    await ShowNotificationIfEnabledAsync(isEnabled, updatedSchedule);
                    return true;
                }
            }
            // Android: Permission granted or NotificationEnabled is false - proceed normally
#else
            // iOS and other platforms: Permission is required for IsEnabled
            if (!await CheckNotificationPermissionsAsync(scheduleId))
            {
                return false;
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
        // Get the schedule to check NotificationEnabled status
        var schedule = await alarmScheduleService.GetScheduleByIdAsync(scheduleId, false, false);
        
#if ANDROID
        // Android: Only check notification permission if "Tap to Play" (NotificationEnabled) is enabled
        // The main reminder (IsEnabled) can work without notification permission
        // However, if user enables IsEnabled (reminder) from home page and NotificationEnabled is already true,
        // we need to check/request notification permission because tap-to-play requires it
        if (schedule != null && schedule.NotificationEnabled)
        {
            logger.Information("Android: Schedule {ScheduleId} has NotificationEnabled=true, checking notification permission before enabling reminder", scheduleId);
            var granted = await NotificationPermissionHelper.RequestNotificationPermissionIfNeededAsync();
            if (!granted)
            {
                logger.Warning("Cannot enable schedule {ScheduleId} with NotificationEnabled=true - notification permission denied", scheduleId);
                await toastService.ShowMessage(
                    "Notification permission is required for tap-to-play alarms. Please enable notifications in system settings.",
                    7);
                return false;
            }
            logger.Information("Android: Notification permission granted for schedule {ScheduleId}", scheduleId);
        }
        else if (schedule != null)
        {
            logger.Debug("Android: Schedule {ScheduleId} has NotificationEnabled=false, no permission check needed", scheduleId);
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
            logger.Information("Requesting iOS notification permission for schedule {ScheduleId}", scheduleId);
            
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
                    catch
                    {
                        granted = false;
                    }
                }
                else
                {
                    // Timeout - user didn't respond, assume denied
                    logger.Warning("Permission request timeout for schedule {ScheduleId}", scheduleId);
                    granted = false;
                }
                
                if (!granted)
                {
                    logger.Warning("Cannot enable schedule {ScheduleId} - notification permission denied. iOS requires notification permission for reminders.", scheduleId);
                    await toastService.ShowMessage(
                        "Notification permission is required for reminders on iOS. Please enable notifications in system settings.",
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
        // WinUI and other platforms
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            if (schedule != null && schedule.NotificationEnabled)
            {
                if (await notificationService.CanScheduleAsync())
                {
                    return true;
                }

                logger.Warning("Cannot enable schedule {ScheduleId} with NotificationEnabled=true - notification permission denied", scheduleId);
                await toastService.ShowMessage(
                    "Notification permission is required for tap-to-play alarms. Please enable notifications in system settings.",
                    7);
                return false;
            }
        }
        return true;
#endif
    }
#pragma warning restore CS9113

    private async Task ShowNotificationPermissionMessageAsync()
    {
        var message = DeviceInfo.Platform == DevicePlatform.iOS
            ? "Cannot schedule reminder because you've disabled notifications. " +
              "Please enable notification for this app under system settings."
            : "Cannot schedule reminder because you've denied background apps permission. " +
              "Please grant background apps permission for this app under system settings.";

        await toastService.ShowMessage(message, 7);
    }

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
        var exceptionType = ex.GetType().FullName;
        return exceptionType == "Java.Lang.SecurityException" ||
               ex.Message.Contains("SCHEDULE_EXACT_ALARM") ||
               ex.Message.Contains("USE_EXACT_ALARM");
    }

    private async Task<bool> HandleSecurityExceptionAsync(int scheduleId, Exception ex)
    {
        logger.Error(ex, "SecurityException when updating schedule {ScheduleId}. SCHEDULE_EXACT_ALARM permission may be missing or revoked.", scheduleId);

        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            await toastService.ShowMessage(
                "Cannot schedule reminder. Please enable 'Alarms & reminders' permission in system settings.",
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
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }
        catch (Exception ex)
        {
            // Ignore errors during cancellation/disposal
            logger.Warning(ex, "Error during cancellation token source disposal");
        }

        // All injected services are singletons, so don't dispose them
        // No event handlers to unsubscribe
    }
}

