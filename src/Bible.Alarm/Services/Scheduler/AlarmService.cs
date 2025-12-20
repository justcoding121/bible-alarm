using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Scheduler.Interfaces;

namespace Bible.Alarm.Services.Scheduler;

/// <summary>
/// Service for managing alarm schedules and OS-level toast notifications.
/// Schedules OS toast notifications only when the alarm is enabled.
/// Flyout messages are shown separately via ToastService.ShowScheduledNotification.
/// </summary>
public sealed class AlarmService(
    INotificationService notificationService)
    : IAlarmService, IDisposable
{
    /// <summary>
    /// Creates an alarm schedule and schedules OS toast notification if enabled.
    /// Only schedules if notifications are available (e.g., not in debug mode on Windows).
    /// </summary>
    public async Task Create(AlarmSchedule schedule)
    {
        // Only schedule OS notification if alarm is enabled and notifications are available
        if (schedule.IsEnabled && await notificationService.CanScheduleAsync())
        {
            await ScheduleNotification(schedule);
        }
    }

    /// <summary>
    /// Updates an alarm schedule and reschedules OS toast notification if enabled.
    /// Only schedules if notifications are available (e.g., not in debug mode on Windows).
    /// </summary>
    public async void Update(AlarmSchedule schedule)
    {
        // Remove existing notification first
        if (await notificationService.CanScheduleAsync())
        {
            await RemoveNotification(schedule.Id);
        }

        // Schedule new notification only if alarm is enabled
        if (schedule.IsEnabled && await notificationService.CanScheduleAsync())
        {
            await ScheduleNotification(schedule);
        }
    }

    /// <summary>
    /// Deletes an alarm schedule and removes OS toast notification.
    /// Only removes if notifications are available (e.g., not in debug mode on Windows).
    /// </summary>
    public async void Delete(int scheduleId)
    {
        // Remove notification if available
        if (await notificationService.CanScheduleAsync())
        {
            await RemoveNotification(scheduleId);
        }
    }

    private async Task ScheduleNotification(AlarmSchedule schedule)
    {
        await notificationService.ScheduleNotificationAsync(schedule,
            string.IsNullOrEmpty(schedule.Name) ? "Bible Alarm" : schedule.Name,
            "Press to start listening now.");
    }

    private async Task RemoveNotification(int scheduleId) => await notificationService.RemoveAsync(scheduleId);

    private bool isDisposed;

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // All injected services (notificationService) are singletons, so don't dispose them
        // No event handlers to unsubscribe
    }
}
