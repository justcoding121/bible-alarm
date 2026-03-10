#nullable enable

using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Platforms.Windows.Helpers;
using Bible.Alarm.Platforms.Windows.Services.Handlers.Interfaces;
using Bible.Alarm.Platforms.Windows.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Schedule;
using Serilog;
using Windows.UI.Notifications;

namespace Bible.Alarm.Platforms.Windows.Services.UI;

public sealed partial class WindowsNotificationService(IServiceProvider serviceProvider, ILogger logger) : IWindowsNotificationService
{
    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task ShowNotificationAsync(int scheduleId)
    {
        // Resolve WindowsAlarmHandler lazily to break circular dependency
        // WindowsNotificationService -> WindowsAlarmHandler -> IPlaybackService -> INotificationService
        var windowsAlarmHandler = serviceProvider.GetRequiredService<IWindowsAlarmHandler>();
        await windowsAlarmHandler.HandleAsync(scheduleId, true);
    }

    public Task ScheduleNotificationAsync(AlarmSchedule schedule,
        string title, string body)
    {
        try
        {
            var scheduleId = schedule.Id;

            var notifier = WindowsToastNotifierFactory.GetToastNotifier();
            if (notifier == null)
            {
                logger.Error("Failed to create toast notifier for schedule {ScheduleId}. App may not be properly registered for notifications. " +
                    "Scheduled notifications require the app to be installed as an MSIX package.", scheduleId);
                return Task.CompletedTask;
            }

            // Remove all existing notifications for this schedule before rescheduling
            WindowsScheduledToastManager.RemoveAllNotificationsForSchedule(notifier, scheduleId);

            // Schedule notifications for the next 90 days
            const int daysToSchedule = 90;
            var maxDate = DateTimeOffset.Now.AddDays(daysToSchedule);
            var currentDate = DateTimeOffset.Now;
            var scheduledCount = 0;
            // Safety limit to prevent infinite loops
            const int maxOccurrences = 1000;

            for (int i = 0; i < maxOccurrences; i++)
            {
                var fireDate = schedule.NextFireDate(currentDate);

                // Stop if beyond our 90-day window
                if (fireDate > maxDate)
                {
                    break;
                }

                // Skip if in the past (shouldn't happen, but safety check)
                if (fireDate <= DateTimeOffset.Now)
                {
                    currentDate = fireDate;
                    continue;
                }

                // Create unique ID for this occurrence: Windows has a 16-character limit for notification IDs
                // Use format: "{scheduleId}_{hash}" where hash is a short representation of the date/time
                // ScheduleId can be up to 4 digits (9999), so we have ~12 chars for the hash
                var uniqueId = WindowsNotificationIdGenerator.BuildUniqueId(scheduleId, fireDate);
                var toast = WindowsToastXmlFactory.CreateScheduledToast(uniqueId, scheduleId, title, body, fireDate);

                try
                {
                    notifier.AddToSchedule(toast);
                    scheduledCount++;
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "Failed to schedule notification for schedule {ScheduleId} at {FireDate}. Error: {ErrorMessage}",
                        scheduleId, fireDate, ex.Message);
                    // Continue with next occurrence
                }

                currentDate = fireDate;
            }

            if (scheduledCount > 0)
            {
                logger.Debug("Successfully scheduled {Count} notifications for schedule {ScheduleId} (next {Days} days)", scheduledCount, scheduleId, daysToSchedule);
            }
            else
            {
                logger.Warning("No notifications were scheduled for schedule {ScheduleId}. Check alarm schedule configuration.", scheduleId);

                // Log additional diagnostic information
                var scheduledToasts = notifier.GetScheduledToastNotifications();
                logger.Warning("Total scheduled toasts in system: {TotalCount}. Next fire date was: {NextFireDate}",
                    scheduledToasts.Count, schedule.NextFireDate(DateTimeOffset.Now));
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error scheduling notifications for schedule {ScheduleId}", schedule.Id);
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(int scheduleId)
    {
        try
        {
            var notifier = GetToastNotifier();
            if (notifier is null)
            {
                return Task.CompletedTask;
            }

            // Remove all notifications for this schedule (supports multiple occurrences)
            var removedCount = WindowsScheduledToastManager.RemoveAllNotificationsForSchedule(notifier, scheduleId);
            if (removedCount > 0)
            {
                logger.Information("Removed {Count} scheduled notifications for schedule {ScheduleId}", removedCount, scheduleId);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error removing notifications for schedule {ScheduleId}", scheduleId);
        }

        return Task.CompletedTask;
    }

    public Task<bool> IsScheduledAsync(int scheduleId)
    {
        try
        {
            var notifier = GetToastNotifier();
            if (notifier is null)
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(WindowsScheduledToastManager.IsNotificationScheduled(notifier, scheduleId));
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error checking if notification is scheduled for schedule {ScheduleId}", scheduleId);
            return Task.FromResult(false);
        }
    }

    public Task ClearDeliveredNotificationAsync(int scheduleId)
    {
        return Task.CompletedTask;
    }

    public Task<bool> CanScheduleAsync() => Task.FromResult(WindowsBootstrapHelper.IsBackgroundTaskEnabled);

    /// <summary>
    /// Dismisses the media playback toast notification and removes it from the screen.
    /// </summary>
    public void DismissMediaToast()
    {
        try
        {
            var notifier = GetToastNotifier();
            if (notifier == null)
            {
                logger.Debug("Cannot dismiss media toast - toast notifier unavailable");
                return;
            }

            // Remove toast from history using tag and group
            // This removes it from both the action center and dismisses it from the screen if still visible
            try
            {
                ToastNotificationManager.History.Remove("MediaPlayback", "MediaPlayback");
                logger.Debug("Media toast dismissed and removed from screen");
            }
            catch (Exception ex)
            {
                // Toast might not exist in history (e.g., already dismissed or never shown)
                // This is normal and not an error
                logger.Debug(ex, "Toast not found in history (may already be dismissed)");
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error dismissing media toast notification");
        }
    }

    /// <summary>
    /// Shows a rich toast notification with media metadata (artwork, title, subtitle, album).
    /// Uses ToastGeneric template which supports images, multiple text elements, and action buttons.
    /// Windows automatically replaces any existing toast with the same Tag and Group, so no need to dismiss first.
    /// </summary>
    public void ShowMediaToast(string? title, string? subtitle, string? body, string? artworkUrl, bool canPlayNext = false, bool canPlayPrevious = false, bool isPlaying = false)
    {
        try
        {
            var notifier = GetToastNotifier();
            if (notifier == null)
            {
                logger.Debug("Cannot show media toast - toast notifier unavailable");
                return;
            }

            var toastXml = WindowsToastXmlFactory.CreateMediaToastXml(title, subtitle, body, artworkUrl, canPlayNext, canPlayPrevious, isPlaying);
            var toast = new ToastNotification(toastXml);

            // Use the same Tag and Group - Windows will automatically replace any existing toast with these values
            // This allows seamless updates when track changes or play/pause state changes
            toast.Tag = "MediaPlayback";
            toast.Group = "MediaPlayback";

            // Sound is suppressed via silent audio element in the toast XML
            toast.SuppressPopup = false;

            // Show the toast - Windows will automatically replace any existing toast with the same tag/group
            notifier.Show(toast);
            logger.Debug("Media toast shown/updated: Title={Title}, Subtitle={Subtitle}, ArtworkUrl={ArtworkUrl}. Clicking toast will activate existing app instance.",
                title, subtitle, artworkUrl);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error showing media toast notification");
        }
    }

    private static ToastNotifier? GetToastNotifier() => WindowsToastNotifierFactory.GetToastNotifier();
}
