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
        return SequentialMatchingInvoker.InvokeEachMatching(
            scheduledToasts,
            toast => ScheduledToastNotificationIdMatcher.MatchesSchedule(scheduleId, toast.Id),
            toast => notifier.RemoveFromSchedule(toast),
            (toast, ex) =>
                Log.Warning(
                    ex,
                    AppConstants.Logging.WindowsToastFlyoutDiagnosticsLog.FailedRemovingScheduledNotificationForSchedule,
                    toast.Id,
                    scheduleId));
    }
}

