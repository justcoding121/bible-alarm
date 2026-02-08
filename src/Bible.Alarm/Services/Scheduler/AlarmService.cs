using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Services.Scheduler;

/// <summary>
/// Service for managing alarm schedules and OS-level toast notifications.
/// Schedules OS toast notifications only when the alarm is enabled.
/// Flyout messages are shown separately via ToastService.ShowScheduledNotification.
/// </summary>
public sealed class AlarmService(
    INotificationService notificationService)
    : IAlarmService
{
    /// <summary>
    /// Creates an alarm schedule and schedules OS toast notification if enabled.
    /// Only schedules if notifications are available (e.g., not in debug mode on Windows).
    /// </summary>
    public async Task Create(AlarmSchedule schedule)
    {
        // Only schedule OS notification if alarm is enabled, has at least one day selected, and notifications are available
        if (schedule.IsEnabled && schedule.DaysOfWeek != 0 && await notificationService.CanScheduleAsync())
        {
            await ScheduleNotification(schedule);
        }
    }

    /// <summary>
    /// Updates an alarm schedule and reschedules OS toast notification if enabled.
    /// Only schedules if notifications are available (e.g., not in debug mode on Windows).
    /// </summary>
    public async Task Update(AlarmSchedule schedule)
    {
        // Remove existing notification first
        if (await notificationService.CanScheduleAsync())
        {
            await RemoveNotification(schedule.Id);
        }

        // Schedule new notification only if alarm is enabled and has at least one day selected
        if (schedule.IsEnabled && schedule.DaysOfWeek != 0 && await notificationService.CanScheduleAsync())
        {
            await ScheduleNotification(schedule);
        }
    }

    /// <summary>
    /// Deletes an alarm schedule and removes OS toast notification.
    /// Only removes if notifications are available (e.g., not in debug mode on Windows).
    /// </summary>
    public async Task Delete(int scheduleId)
    {
        // Remove notification if available
        if (await notificationService.CanScheduleAsync())
        {
            await RemoveNotification(scheduleId);
        }
    }

    private async Task ScheduleNotification(AlarmSchedule schedule)
    {
        // Use schedule name if available, otherwise use empty string (not "Bible Alarm")
        var title = string.IsNullOrWhiteSpace(schedule.Name) ? string.Empty : schedule.Name;
        await notificationService.ScheduleNotificationAsync(schedule,
            title,
            "Press to start listening now.");
    }

    private async Task RemoveNotification(int scheduleId) => await notificationService.RemoveAsync(scheduleId);

}
