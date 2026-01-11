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
        if (isEnabled && !await CheckNotificationPermissionsAsync(scheduleId))
        {
            return false;
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
        // Check Android notification permission if NotificationEnabled is true
        // Get the schedule to check if NotificationEnabled is true
        var schedule = await alarmScheduleService.GetScheduleByIdAsync(scheduleId, false, false);
        if (schedule != null && schedule.NotificationEnabled)
        {
            var granted = await NotificationPermissionHelper.RequestNotificationPermissionIfNeededAsync();
            if (!granted)
            {
                logger.Warning("Cannot enable schedule {ScheduleId} with NotificationEnabled=true - notification permission denied", scheduleId);
                await toastService.ShowMessage(
                    "Notification permission is required for tap-to-play alarms. Please enable notifications in system settings.",
                    7);
                return false;
            }
        }
        return true; // Android permission check passed or not needed
#else
        // Check iOS/WinUI notification permissions
        if (DeviceInfo.Platform == DevicePlatform.iOS || DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            if (await notificationService.CanScheduleAsync())
            {
                return true;
            }

            await ShowNotificationPermissionMessageAsync();
            return false;
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

