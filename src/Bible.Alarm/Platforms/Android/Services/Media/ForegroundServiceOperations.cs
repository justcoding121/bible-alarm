#nullable enable

using System;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Support.V4.Media.Session;
using AndroidX.Core.App;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Media;

/// <summary>
/// Handles foreground service start, update, and stop operations.
/// </summary>
internal static class ForegroundServiceOperations
{
    private static readonly ILogger logger = Log.ForContext(typeof(ForegroundServiceOperations));

    public static void StartForeground(Service service, MediaSessionCompat mediaSession)
    {
        try
        {
            // Ensure notification channel exists
            AndroidAutoNotificationHelper.CreateNotificationChannel(service);
            
            // Create notification with MediaSession attached
            var notification = AndroidAutoNotificationHelper.CreateNotification(service, mediaSession);
            
            // Start foreground service with sticky notification
            // Use ForegroundService.TypeMediaPlayback to match MediaElement's type
            if (OperatingSystem.IsAndroidVersionAtLeast(29))
            {
                service.StartForeground(AndroidAutoNotificationHelper.NotificationId, notification, ForegroundService.TypeMediaPlayback);
            }
            else
            {
                service.StartForeground(AndroidAutoNotificationHelper.NotificationId, notification);
            }
            
            logger.Information("Android Auto foreground service started with notification ID {NotificationId}", 
                AndroidAutoNotificationHelper.NotificationId);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error starting Android Auto foreground service");
        }
    }

    public static void UpdateForeground(Service service, MediaSessionCompat mediaSession)
    {
        try
        {
            var notification = AndroidAutoNotificationHelper.CreateNotification(service, mediaSession);
            var notificationManager = NotificationManagerCompat.From(service);
            notificationManager?.Notify(AndroidAutoNotificationHelper.NotificationId, notification);
            logger.Debug("Android Auto foreground notification updated (ID: {NotificationId})", 
                AndroidAutoNotificationHelper.NotificationId);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error updating Android Auto foreground notification");
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
            notificationManager?.Cancel(AndroidAutoNotificationHelper.NotificationId);
            
            logger.Information("Android Auto foreground service stopped and notification cancelled (ID: {NotificationId})", 
                AndroidAutoNotificationHelper.NotificationId);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error stopping Android Auto foreground service");
        }
    }
}
