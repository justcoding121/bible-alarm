using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Models.Schedule;
using Serilog;
#if ANDROID
using Bible.Alarm.Platforms.Android.Services.Helpers;
#elif IOS
using Bible.Alarm.Platforms.iOS.Services.Helpers;
#endif

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
        if (schedule.IsEnabled && schedule.DaysOfWeek != 0 && await notificationService.CanScheduleAsync()
            && ShouldScheduleNotification(schedule))
        {
            await ScheduleNotification(schedule);
        }
    }

    private static bool ShouldScheduleNotification(AlarmSchedule schedule)
    {
#if ANDROID
        // Android: If NotificationEnabled is true but permission is not granted, treat as NotificationEnabled = false
        // The notification will still be scheduled, but it will behave as if tap-to-play is disabled
        if (schedule.NotificationEnabled)
        {
            try
            {
                var permissionService = NotificationPermissionService.Instance;
                bool isPermissionGranted = false;
                try
                {
                    isPermissionGranted = permissionService.IsGranted;
                }
                catch (Exception ex)
                {
                    Log.Logger.Debug(ex, "AlarmService: Android permission check failed, assuming not granted");
                    isPermissionGranted = false;
                }

                if (!isPermissionGranted)
                {
                    // Permission not granted - treat NotificationEnabled as false for scheduling purposes
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex, "AlarmService: Permission service access failed, treating NotificationEnabled as false");
                return false;
            }
        }
        // Android: Schedule notification if IsEnabled is true
        return schedule.IsEnabled;
#elif IOS
        // iOS: Check IsEnabled (reminder enabled) + permission granted
        // If IsEnabled is true but permission is not granted, don't schedule notification
        if (schedule.IsEnabled)
        {
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
                    Log.Logger.Debug(ex, "AlarmService: iOS permission check failed, assuming not granted");
                    isPermissionGranted = false;
                }

                // Only schedule if permission is granted
                return isPermissionGranted;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex, "AlarmService: iOS permission service access failed, not scheduling");
                return false;
            }
        }
        return false;
#else
        // Other platforms: Schedule if IsEnabled is true
        return schedule.IsEnabled;
#endif
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

        // Schedule new notification only if alarm is enabled, has at least one day selected, and permission check passes
        if (schedule.IsEnabled && schedule.DaysOfWeek != 0 && await notificationService.CanScheduleAsync()
            && ShouldScheduleNotification(schedule))
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
        
#if ANDROID
        // Android: If NotificationEnabled is true but permission is not granted, create a temporary schedule
        // with NotificationEnabled=false so the notification behaves as if tap-to-play is disabled
        AlarmSchedule scheduleToUse = schedule;
        if (schedule.NotificationEnabled)
        {
            try
            {
                var permissionService = NotificationPermissionService.Instance;
                bool isPermissionGranted = false;
                try
                {
                    isPermissionGranted = permissionService.IsGranted;
                }
                catch (Exception ex)
                {
                    Log.Logger.Debug(ex, "AlarmService: Permission check failed in ScheduleNotification, assuming not granted");
                    isPermissionGranted = false;
                }

                if (!isPermissionGranted)
                {
                    // Create a temporary schedule with NotificationEnabled=false for scheduling purposes
                    // This ensures the notification is scheduled but behaves as if tap-to-play is disabled
                    scheduleToUse = new AlarmSchedule
                    {
                        Id = schedule.Id,
                        Name = schedule.Name,
                        IsEnabled = schedule.IsEnabled,
                        Hour = schedule.Hour,
                        Minute = schedule.Minute,
                        Second = schedule.Second,
                        DaysOfWeek = schedule.DaysOfWeek,
                        NotificationEnabled = false, // Treat as false when permission not granted
                        MusicEnabled = schedule.MusicEnabled,
                        SnoozeMinutes = schedule.SnoozeMinutes,
                        NumberOfTracksToPlay = schedule.NumberOfTracksToPlay,
                        AlwaysPlayFromStart = schedule.AlwaysPlayFromStart,
                        BiblePublicationSchedule = schedule.BiblePublicationSchedule,
                        Music = schedule.Music
                    };
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex, "AlarmService: Permission service access failed in ScheduleNotification, using schedule as-is");
                scheduleToUse = schedule;
            }
        }

        await notificationService.ScheduleNotificationAsync(scheduleToUse,
            title,
            "Press to start listening now.");
#else
        await notificationService.ScheduleNotificationAsync(schedule,
            title,
            "Press to start listening now.");
#endif
    }

    private async Task RemoveNotification(int scheduleId) => await notificationService.RemoveAsync(scheduleId);

}
