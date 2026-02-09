#nullable enable

using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
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

public sealed class ScheduleStateService(
    ILogger logger,
    IAlarmScheduleService alarmScheduleService,
    IAlarmService alarmService,
    INotificationService notificationService,
    IToastService toastService,
    IDispatcher dispatcher,
    INavigationService navigationService,
    IServiceProvider serviceProvider)
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
            // enable IsEnabled but keep NotificationEnabled=true in DB and show permission modal
            // Android reminder can work without notification permission
            var schedule = await alarmScheduleService.GetScheduleByIdAsync(scheduleId, false, false);
            if (schedule != null && schedule.NotificationEnabled)
            {
                // Check permission status without waiting (non-blocking)
                // Permission requests are handled by ViewModels via the modal
                var granted = NotificationPermissionHelper.IsNotificationPermissionGranted();
                if (!granted)
                {
                    logger.Information("Schedule {ScheduleId} has NotificationEnabled=true but permission not granted - enabling reminder and showing permission modal", scheduleId);
                    
                    // Enable IsEnabled but keep NotificationEnabled=true in DB
                    // Show permission modal - after dismissal, DB will be updated based on permission result
                    AlarmSchedule? androidUpdatedSchedule = null;
                    try
                    {
                        androidUpdatedSchedule = await alarmScheduleService.UpdateScheduleByIdAsync(
                            scheduleId,
                            s => 
                            {
                                s.IsEnabled = isEnabled;
                                // Keep NotificationEnabled=true in DB - will be updated after modal dismissal
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
                    
                    await alarmService.Update(androidUpdatedSchedule);
                    UpdateFluxorStore(androidUpdatedSchedule);
                    await ShowNotificationIfEnabledAsync(isEnabled, androidUpdatedSchedule);
                    
                    // Show notification permission modal
                    // Note: NotificationEnabled will only be updated in DB when user saves from schedule page
                    // Here we only update state (not DB) based on permission result
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        try
                        {
                            var notificationViewModel = new NotificationPermissionViewModel(
                                logger,
                                navigationService,
                                serviceProvider,
                                onModalDismissed: (permissionGranted) =>
                                {
                                    MainThread.BeginInvokeOnMainThread(() =>
                                    {
                                        try
                                        {
                                            // Update NotificationEnabled in state only (not DB)
                                            // DB will be updated when user saves from schedule page
                                            // We need to update both CurrentSchedule (if it matches) and the schedule in Schedules collection
                                            var state = serviceProvider.GetRequiredService<IState<ApplicationState>>();
                                            var schedules = state.Value.Schedules;
                                            var scheduleToUpdate = schedules?.FirstOrDefault(s => s.Id == scheduleId);
                                            
                                            if (scheduleToUpdate != null)
                                            {
                                                // Clone schedule and set NotificationEnabled based on permission
                                                var updatedSchedule = ScheduleStateHelper.CloneScheduleStateItem(scheduleToUpdate);
                                                updatedSchedule.NotificationEnabled = permissionGranted;
                                                
                                                // Dispatch update action to set NotificationEnabled in state
                                                // Use shouldSave: true to update Schedules collection (for warning button visibility)
                                                // but the effect won't save to DB because NotificationEnabled change doesn't trigger DB save
                                                // Actually, we need shouldSave: false to avoid DB save, but that only updates CurrentSchedule
                                                // For now, use shouldSave: false - CurrentSchedule update will sync to Schedules via OnStateChanged
                                                dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, false, shouldSave: false));
                                                
                                                logger.Information("EnableScheduleAsync: Permission {PermissionStatus} from modal - set NotificationEnabled to {NotificationEnabled} in state for schedule {ScheduleId}. DB will be updated on save.", 
                                                    permissionGranted ? "granted" : "denied", permissionGranted, scheduleId);
                                            }
                                            else
                                            {
                                                logger.Warning("EnableScheduleAsync: Schedule not found in Schedules collection when trying to set NotificationEnabled. ScheduleId={ScheduleId}", 
                                                    scheduleId);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            logger.Error(ex, "EnableScheduleAsync: Error updating NotificationEnabled in state after permission check");
                                        }
                                    });
                                });
                            
                            notificationViewModel.StartPermissionCheckTimer();
                            await navigationService.OpenNotificationPermissionModalAsync(notificationViewModel);
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, "EnableScheduleAsync: Error opening notification permission modal");
                        }
                    });
                    
                    return true;
                }
            }
            // Android: Permission granted or NotificationEnabled is false - proceed normally
#elif IOS
            // iOS: If IsEnabled is true but permission is denied,
            // enable IsEnabled but keep it true in DB and show permission modal
            // iOS reminder requires notification permission
            try
            {
                var permissionService = IOSNotificationPermissionService.Instance;
                bool isPermissionGranted = false;
                try
                {
                    isPermissionGranted = permissionService.IsGranted;
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "EnableScheduleAsync (iOS): Exception checking permission - assuming not granted");
                    isPermissionGranted = false;
                }

                if (!isPermissionGranted)
                {
                    logger.Information("Schedule {ScheduleId} has IsEnabled=true but permission not granted (iOS) - enabling reminder and showing permission modal", scheduleId);
                    
                    // Enable IsEnabled but keep it true in DB
                    // Show permission modal - after dismissal, DB will be updated based on permission result
                    AlarmSchedule? iosUpdatedSchedule = null;
                    try
                    {
                        iosUpdatedSchedule = await alarmScheduleService.UpdateScheduleByIdAsync(
                            scheduleId,
                            s => 
                            {
                                s.IsEnabled = isEnabled;
                                // Keep IsEnabled=true in DB - will be updated after modal dismissal
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
                    
                    await alarmService.Update(iosUpdatedSchedule);
                    UpdateFluxorStore(iosUpdatedSchedule);
                    await ShowNotificationIfEnabledAsync(isEnabled, iosUpdatedSchedule);
                    
                    // Show notification permission modal
                    // Note: IsEnabled will only be updated in DB when user saves from schedule page
                    // Here we only update state (not DB) based on permission result
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        try
                        {
                            var notificationViewModel = new NotificationPermissionViewModel(
                                logger,
                                navigationService,
                                serviceProvider,
                                onModalDismissed: (permissionGranted) =>
                                {
                                    MainThread.BeginInvokeOnMainThread(async () =>
                                    {
                                        try
                                        {
                                            // Update IsEnabled in DB (iOS: IsEnabled is updated in DB when toggling from home page)
                                            // Both Android and iOS: IsEnabled is updated in DB when toggling from home page
                                            // Android only: NotificationEnabled is only updated by schedule save
                                            try
                                            {
                                                var dbUpdatedSchedule = await alarmScheduleService.UpdateScheduleByIdAsync(
                                                    scheduleId,
                                                    s => s.IsEnabled = permissionGranted,
                                                    cancellationTokenSource.Token);
                                                
                                                // Update Fluxor store with DB value
                                                UpdateFluxorStore(dbUpdatedSchedule);
                                                
                                                logger.Information("EnableScheduleAsync (iOS): Permission {PermissionStatus} from modal - set IsEnabled to {IsEnabled} in DB for schedule {ScheduleId}.", 
                                                    permissionGranted ? "granted" : "denied", permissionGranted, scheduleId);
                                            }
                                            catch (Exception ex)
                                            {
                                                logger.Error(ex, "EnableScheduleAsync (iOS): Error updating IsEnabled in DB after permission check");
                                                // Fallback: update state only if DB update fails
                                                var state = serviceProvider.GetRequiredService<IState<ApplicationState>>();
                                                var schedules = state.Value.Schedules;
                                                var scheduleToUpdate = schedules?.FirstOrDefault(s => s.Id == scheduleId);
                                                
                                                if (scheduleToUpdate != null)
                                                {
                                                    var updatedSchedule = ScheduleStateHelper.CloneScheduleStateItem(scheduleToUpdate);
                                                    updatedSchedule.IsEnabled = permissionGranted;
                                                    dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, false, shouldSave: false));
                                                }
                                                else
                                                {
                                                    logger.Warning("EnableScheduleAsync (iOS): Schedule not found in Schedules collection when trying to set IsEnabled. ScheduleId={ScheduleId}", 
                                                        scheduleId);
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            logger.Error(ex, "EnableScheduleAsync (iOS): Error updating IsEnabled after permission check");
                                        }
                                    });
                                });
                            
                            notificationViewModel.StartPermissionCheckTimer();
                            await navigationService.OpenNotificationPermissionModalAsync(notificationViewModel);
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, "EnableScheduleAsync (iOS): Error opening notification permission modal");
                        }
                    });
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "EnableScheduleAsync (iOS): Exception checking notification permission");
                // If permission check fails, proceed normally (enable IsEnabled)
            }
            // iOS: Permission granted - proceed normally
#else
            // Other platforms: Permission is required for IsEnabled
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
            // Check permission status without waiting (non-blocking)
            // Permission requests are handled by ViewModels via the modal
            var granted = NotificationPermissionHelper.IsNotificationPermissionGranted();
            if (!granted)
            {
                logger.Warning("Cannot enable schedule {ScheduleId} with NotificationEnabled=true - notification permission denied", scheduleId);
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

