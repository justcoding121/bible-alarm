#nullable enable

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Support.V4.Media.Session;
using AndroidX.Core.App;
using Serilog;
using Application = Android.App.Application;

namespace Bible.Alarm.Platforms.Android.Services.Media;

/// <summary>
/// Handles foreground service start, update, and stop operations.
/// </summary>
internal static class ForegroundServiceOperations
{
    private static readonly ILogger logger = Log.ForContext(typeof(ForegroundServiceOperations));

    /// <summary>
    /// Starts a minimal foreground service immediately with a bare notification.
    /// Called as the very first thing in OnCreate() to prevent the OS from killing the process.
    /// The notification will be replaced later with a proper MediaStyle notification.
    /// </summary>
    public static void StartForegroundMinimal(Service service)
    {
        try
        {
            ForegroundNotificationHelper.CreateNotificationChannel(service);
            var notification = ForegroundNotificationHelper.CreateMinimalNotification(service);

            if (OperatingSystem.IsAndroidVersionAtLeast(29))
            {
                service.StartForeground(ForegroundNotificationHelper.NotificationId, notification, ForegroundService.TypeMediaPlayback);
            }
            else
            {
                service.StartForeground(ForegroundNotificationHelper.NotificationId, notification);
            }

            logger.Information("Minimal foreground service started immediately (pre-bootstrap)");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error starting minimal foreground service");
        }
    }

    public static void StartForeground(Service service, MediaSessionCompat mediaSession, bool isAlarmNotification = false)
    {
        try
        {
            // Ensure notification channel exists
            ForegroundNotificationHelper.CreateNotificationChannel(service);

            // Create notification with MediaSession attached
            var notification = ForegroundNotificationHelper.CreateNotification(service, mediaSession, isAlarmNotification);

            // Start foreground service with sticky notification
            // Use ForegroundService.TypeMediaPlayback to match MediaElement's type
            if (OperatingSystem.IsAndroidVersionAtLeast(29))
            {
                service.StartForeground(ForegroundNotificationHelper.NotificationId, notification, ForegroundService.TypeMediaPlayback);
            }
            else
            {
                service.StartForeground(ForegroundNotificationHelper.NotificationId, notification);
            }

            var serviceType = isAlarmNotification ? "Alarm" : "Android Auto";
            logger.Information("{ServiceType} foreground service started with notification ID {NotificationId}",
                serviceType, ForegroundNotificationHelper.NotificationId);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error starting foreground service");
        }
    }

    public static void UpdateForeground(Service service, MediaSessionCompat mediaSession)
    {
        try
        {
            // Check if this is an alarm service to preserve alarm notification style
            bool isAlarm = service is AlarmForegroundService;

            var notification = ForegroundNotificationHelper.CreateNotification(service, mediaSession, isAlarm);
            var notificationManager = NotificationManagerCompat.From(service);
            notificationManager?.Notify(ForegroundNotificationHelper.NotificationId, notification);

            var serviceType = isAlarm ? "Alarm" : "Android Auto";
            logger.Debug("{ServiceType} foreground notification updated (ID: {NotificationId})",
                serviceType, ForegroundNotificationHelper.NotificationId);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error updating foreground notification");
        }
    }

    public static void StopForeground(Service? service)
    {
        try
        {
            if (service == null)
            {
                return;
            }

            // Stop foreground service - this removes the notification
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                service.StopForeground(StopForegroundFlags.Remove);
            }
            else
            {
                service.StopForeground(true);
            }

            // Explicitly cancel the notification to ensure it disappears
            var notificationManager = NotificationManagerCompat.From(service);
            notificationManager?.Cancel(ForegroundNotificationHelper.NotificationId);

            logger.Information("Foreground service stopped and notification cancelled (ID: {NotificationId})",
                ForegroundNotificationHelper.NotificationId);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error stopping Android Auto foreground service");
        }
    }

    /// <summary>
    /// Cancels our foreground notification (ID 2) using application context.
    /// Call when MediaElement takes over so no "Bible Alarm" notification remains during playback.
    /// Ensures a clean lock screen with only the MediaElement media notification.
    /// </summary>
    public static void CancelForegroundNotificationFromAppContext()
    {
        try
        {
            var context = Application.Context;
            if (context == null)
            {
                logger.Debug("Application.Context is null - cannot cancel foreground notification from app context");
                return;
            }

            var notificationManager = NotificationManagerCompat.From(context);
            if (notificationManager != null)
            {
                notificationManager.Cancel(ForegroundNotificationHelper.NotificationId);
                logger.Debug("Cancelled foreground notification (ID: {NotificationId}) from application context",
                    ForegroundNotificationHelper.NotificationId);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error cancelling foreground notification from application context");
        }
    }
}
