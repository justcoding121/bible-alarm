#nullable enable

using System.Linq;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Serilog;
using Windows.UI.Notifications;

namespace Bible.Alarm.Platforms.Windows.Services.UI;

internal static class WindowsScheduledToastManager
{
    internal static bool IsNotificationScheduled(ToastNotifier notifier, int scheduleId)
    {
        // Check if any notification exists for this schedule
        // Notification IDs are in format: "{scheduleId}_{hash}" (max 16 characters total)
        var scheduledToasts = notifier.GetScheduledToastNotifications();
        return scheduledToasts.Any(toast => ScheduledToastNotificationIdMatcher.MatchesSchedule(scheduleId, toast.Id));
    }

    /// <summary>
    /// Removes all scheduled notifications for a given schedule ID.
    /// Supports both old format (just scheduleId) and new format ({scheduleId}_{hash}).
    /// </summary>
    internal static int RemoveAllNotificationsForSchedule(ToastNotifier notifier, int scheduleId)
    {
        var scheduledToasts = notifier.GetScheduledToastNotifications();
        var toRemove = new List<ScheduledToastNotification>();

        // Find all notifications for this schedule
        foreach (var toast in scheduledToasts)
        {
            // Support both old format (just scheduleId) and new format ({scheduleId}_{ticks})
            if (ScheduledToastNotificationIdMatcher.MatchesSchedule(scheduleId, toast.Id))
            {
                toRemove.Add(toast);
            }
        }

        // Remove all found notifications
        foreach (var toast in toRemove)
        {
            try
            {
                notifier.RemoveFromSchedule(toast);
            }
            catch (Exception ex)
            {
                // Log but continue removing others
                Log.Warning(ex, AppConstants.Logging.WindowsToastFlyoutDiagnosticsLog.FailedRemovingScheduledNotificationForSchedule, toast.Id, scheduleId);
            }
        }

        return toRemove.Count;
    }
}

