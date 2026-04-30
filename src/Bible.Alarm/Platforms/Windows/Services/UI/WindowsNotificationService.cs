#nullable enable

using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Platforms.Windows.Helpers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Platforms.Windows.Services.Handlers.Interfaces;
using Bible.Alarm.Platforms.Windows.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Schedule;
using Serilog;
using Windows.UI.Notifications;

namespace Bible.Alarm.Platforms.Windows.Services.UI;

public sealed partial class WindowsNotificationService(IServiceProvider serviceProvider, ILogger logger) : IWindowsNotificationService
{
    /// <summary>
    /// Group used for alarm/schedule toasts so we can remove delivered notifications from Action Center by schedule.
    /// </summary>
    internal const string AlarmToastGroup = "BibleAlarmSchedule";

    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task ShowNotificationAsync(int scheduleId)
    {
        // Resolve WindowsAlarmHandler lazily to break circular dependency
        // WindowsNotificationService -> WindowsAlarmHandler -> IPlaybackService -> INotificationService
        var windowsAlarmHandler = serviceProvider.GetRequiredService<IWindowsAlarmHandler>();
        await windowsAlarmHandler.HandleAsync(scheduleId, true);
    }

    public Task ScheduleNotificationAsync(AlarmSchedule alarmSchedule,
        string title, string body)
    {
        try
        {
            var scheduleId = alarmSchedule.Id;

            var notifier = WindowsToastNotifierFactory.GetToastNotifier();
            if (notifier == null)
            {
                logger.Error(AppConstants.Logging.MauiPlatformUiDiagnosticsLog.WindowsNotificationFailedToastNotifierScheduleMsixHint, scheduleId);
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
                var fireDate = alarmSchedule.NextFireDate(currentDate);

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
                    logger.Warning(ex, AppConstants.Logging.WindowsNotificationServiceDiagnosticsLog.FailedToScheduleNotificationAtFireDate,
                        scheduleId, fireDate, ex.Message);
                    // Continue with next occurrence
                }

                currentDate = fireDate;
            }

            if (scheduledCount > 0)
            {
                logger.Debug(AppConstants.Logging.WindowsNotificationServiceDiagnosticsLog.SuccessfullyScheduledCountForScheduleDays, scheduledCount, scheduleId, daysToSchedule);
            }
            else
            {
                logger.Warning(AppConstants.Logging.WindowsNotificationServiceDiagnosticsLog.NoNotificationsScheduledCheckConfiguration, scheduleId);

                // Log additional diagnostic information
                var scheduledToasts = notifier.GetScheduledToastNotifications();
                logger.Warning(AppConstants.Logging.WindowsNotificationServiceDiagnosticsLog.ScheduledToastTotalsWithPriorNextFireDate,
                    scheduledToasts.Count, alarmSchedule.NextFireDate(DateTimeOffset.Now));
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.WindowsNotificationServiceDiagnosticsLog.ErrorSchedulingNotificationsForSchedule, alarmSchedule.Id);
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
                logger.Information(AppConstants.Logging.WindowsNotificationServiceDiagnosticsLog.RemovedCountScheduledNotificationsForSchedule, removedCount, scheduleId);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.WindowsNotificationServiceDiagnosticsLog.ErrorRemovingNotificationsForSchedule, scheduleId);
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
            logger.Error(ex, AppConstants.Logging.WindowsNotificationServiceDiagnosticsLog.ErrorCheckingIfNotificationScheduledForSchedule, scheduleId);
            return Task.FromResult(false);
        }
    }

    public Task ClearDeliveredNotificationAsync(int scheduleId)
    {
        try
        {
            var tag = scheduleId.ToString();
            ToastNotificationManager.History.Remove(tag, AlarmToastGroup);
            logger.Debug(AppConstants.Logging.WindowsNotificationServiceDiagnosticsLog.ClearedDeliveredAlarmFromActionCenter, scheduleId);
        }
        catch (Exception ex)
        {
            // Toast may not be in history (e.g. already dismissed or never shown)
            logger.Debug(ex, AppConstants.Logging.WindowsNotificationServiceDiagnosticsLog.NoDeliveredAlarmToastInHistory, scheduleId);
        }

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
                logger.Debug(AppConstants.Logging.WindowsNotificationServiceDiagnosticsLog.CannotDismissMediaToastNotifierUnavailable);
                return;
            }

            // Remove toast from history using tag and group
            // This removes it from both the action center and dismisses it from the screen if still visible
            try
            {
                ToastNotificationManager.History.Remove("MediaPlayback", "MediaPlayback");
                logger.Debug(AppConstants.Logging.WindowsNotificationServiceDiagnosticsLog.MediaToastDismissedRemovedFromScreen);
            }
            catch (Exception ex)
            {
                // Toast might not exist in history (e.g., already dismissed or never shown)
                // This is normal and not an error
                logger.Debug(ex, AppConstants.Logging.WindowsNotificationServiceDiagnosticsLog.ToastNotFoundInHistoryMayBeDismissed);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.WindowsNotificationServiceDiagnosticsLog.ErrorDismissingMediaToastNotification);
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
                logger.Debug(AppConstants.Logging.WindowsNotificationServiceDiagnosticsLog.CannotShowMediaToastNotifierUnavailable);
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
            logger.Debug(AppConstants.Logging.WindowsNotificationServiceDiagnosticsLog.MediaToastShownUpdatedClickActivatesApp,
                title, subtitle, artworkUrl);
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.WindowsNotificationServiceDiagnosticsLog.ErrorShowingMediaToastNotification);
        }
    }

    private static ToastNotifier? GetToastNotifier() => WindowsToastNotifierFactory.GetToastNotifier();
}
