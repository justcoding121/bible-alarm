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
        // Notification IDs are "{scheduleId}_{hash}" (Windows 16-character limit)
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

