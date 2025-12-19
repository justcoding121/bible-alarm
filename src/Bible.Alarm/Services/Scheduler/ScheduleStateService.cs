using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Actions.Schedule;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Scheduler;

public class ScheduleStateService(
    ILogger logger,
    IAlarmScheduleService alarmScheduleService,
    IAlarmService alarmService,
    INotificationService notificationService,
    IToastService toastService,
    IDispatcher dispatcher)
    : IScheduleStateService, IDisposable
{
    private readonly ILogger _logger = logger;
    private readonly IAlarmScheduleService _alarmScheduleService = alarmScheduleService;
    private readonly IAlarmService _alarmService = alarmService;
    private readonly INotificationService _notificationService = notificationService;
    private readonly IToastService _toastService = toastService;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private bool _isDisposed;

    public async Task<bool> UpdateScheduleEnabledStateAsync(int scheduleId, bool isEnabled)
    {
        // Check notification permissions if enabling
        if (isEnabled &&
            (DeviceInfo.Platform == DevicePlatform.iOS
             || DeviceInfo.Platform == DevicePlatform.WinUI)
            && !await _notificationService.CanScheduleAsync())
        {
            if (DeviceInfo.Platform == DevicePlatform.iOS)
            {
                await _toastService.ShowMessage(
                    "Cannot schedule reminder because you've disabled notifications. " +
                    "Please enable notification for this app under system settings.", 7);
            }
            else
            {
                await _toastService.ShowMessage(
                    "Cannot schedule reminder because you've denied background apps permission. " +
                    "Please grant background apps permission for this app under system settings.", 7);
            }

            // Indicates the state change was rejected
            return false;
        }

        // Update database and alarm service
        AlarmSchedule updatedSchedule = null;
        try
        {
            updatedSchedule = await _alarmScheduleService.UpdateScheduleByIdAsync(
                scheduleId,
                schedule => schedule.IsEnabled = isEnabled,
                _cancellationTokenSource.Token);

            await Task.Run(() =>
            {
                _alarmService.Update(updatedSchedule);
            });
        }
        catch (Exception ex)
        {
            // Check if it's a SecurityException (Android exact alarm permission issue)
            var exceptionType = ex.GetType().FullName;
            if (exceptionType == "Java.Lang.SecurityException" || ex.Message.Contains("SCHEDULE_EXACT_ALARM") || ex.Message.Contains("USE_EXACT_ALARM"))
            {
                _logger.Error(ex, "SecurityException when updating schedule {ScheduleId}. SCHEDULE_EXACT_ALARM permission may be missing or revoked.", scheduleId);

                // Show user-friendly message for Android
                if (DeviceInfo.Platform == DevicePlatform.Android)
                {
                    await _toastService.ShowMessage(
                        "Cannot schedule reminder. Please enable 'Alarms & reminders' permission in system settings.",
                        7);
                }

                // Return false to indicate the state change was rejected
                return false;
            }

            // Re-throw if it's a different exception
            throw;
        }

        // Update the Fluxor store to trigger state change and UI refresh
        if (updatedSchedule != null)
        {
            _dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
        }

        // Show notification if enabled
        if (isEnabled && updatedSchedule != null)
        {
            await _toastService.ShowScheduledNotification(updatedSchedule);
        }

        // Indicates the state change was successful
        return true;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        // Cancel and dispose cancellation token source
        try
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
        catch (Exception ex)
        {
            // Ignore errors during cancellation/disposal
            _logger.Warning(ex, "Error during cancellation token source disposal");
        }

        // All injected services are singletons, so don't dispose them
        // No event handlers to unsubscribe
    }
}

