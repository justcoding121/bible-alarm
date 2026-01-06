#nullable enable

using System;
using _Microsoft.Android.Resource.Designer;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using AndroidX.Core.App;
using AndroidX.Core.Graphics.Drawable;
using AndroidX.Media;
using AndroidX.Media.App;
using Serilog;
using Application = Android.App.Application;
using NotificationCompat = AndroidX.Core.App.NotificationCompat;
using MediaStyle = AndroidX.Media.App.NotificationCompat.MediaStyle;

namespace Bible.Alarm.Platforms.Android.Services.Media;

/// <summary>
/// Handles Android Auto notification creation and channel management.
/// </summary>
internal static class AndroidAutoNotificationHelper
{
    private static readonly ILogger logger = Log.ForContext(typeof(AndroidAutoNotificationHelper));
    private const int AndroidAutoNotificationId = 2; // MediaElement uses 1, so we use 2
    private const string AndroidAutoChannelId = "android_auto_channel";
    private const string AndroidAutoChannelName = "Android Auto Connection";

    public static int NotificationId => AndroidAutoNotificationId;

    public static void CreateNotificationChannel(Service service)
    {
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                var notificationManager = service.GetSystemService(Service.NotificationService) as NotificationManager;
                if (notificationManager != null)
                {
                    var channel = new NotificationChannel(
                        AndroidAutoChannelId,
                        AndroidAutoChannelName,
                        NotificationImportance.Low); // Low priority when idle
                    
                    channel.Description = "Keeps Android Auto connection alive";
                    channel.SetShowBadge(false);
                    channel.EnableLights(false);
                    channel.EnableVibration(false);
                    
                    notificationManager.CreateNotificationChannel(channel);
                    logger.Debug("Created notification channel: {ChannelId}", AndroidAutoChannelId);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error creating notification channel (may already exist)");
        }
    }

    public static Notification CreateNotification(Service service, MediaSessionCompat mediaSession)
    {
        var context = service.ApplicationContext ?? Application.Context ?? throw new InvalidOperationException("Context cannot be null");
        
        // Create pending intent for notification tap
        var packageName = context.PackageName ?? throw new InvalidOperationException("PackageName cannot be null");
        var packageManager = context.PackageManager ?? throw new InvalidOperationException("PackageManager cannot be null");
        var launchIntent = packageManager.GetLaunchIntentForPackage(packageName) 
            ?? throw new InvalidOperationException("Launch intent cannot be null");
        
        launchIntent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
        var pendingIntent = PendingIntent.GetActivity(
            context, 
            0, 
            launchIntent, 
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)
            ?? throw new InvalidOperationException("PendingIntent cannot be null");

        // Get metadata from MediaSession for notification content
        var metadata = mediaSession?.Controller?.Metadata;
        var title = metadata?.GetString(MediaMetadataCompat.MetadataKeyTitle) ?? "Bible Alarm";
        var artist = metadata?.GetString(MediaMetadataCompat.MetadataKeyArtist) ?? "Ready to play";
        var artwork = metadata?.GetBitmap(MediaMetadataCompat.MetadataKeyArt);

        // Create notification builder - context is guaranteed non-null at this point
        // Split the fluent API chain to avoid compiler warnings
        var builder = new NotificationCompat.Builder(context!, AndroidAutoChannelId);
        builder.SetSmallIcon(ResourceConstant.Drawable.exo_icon_circular_play);
        builder.SetContentTitle(title);
        builder.SetContentText(artist);
        builder.SetContentIntent(pendingIntent);
        builder.SetAutoCancel(false); // Sticky notification
        builder.SetOngoing(true); // Makes it sticky/ongoing
        builder.SetForegroundServiceBehavior(NotificationCompat.ForegroundServiceImmediate);
        builder.SetVisibility(NotificationCompat.VisibilityPublic);
        builder.SetPriority(NotificationCompat.PriorityLow); // Low priority when idle
        builder.SetShowWhen(false);
        builder.SetOnlyAlertOnce(true);

        // Create play action button that triggers MediaSession.OnPlay()
        var playIntent = AndroidX.Media.Session.MediaButtonReceiver.BuildMediaButtonPendingIntent(
            context!,
            PlaybackStateCompat.ActionPlay);
        
        if (playIntent != null)
        {
            var playIcon = IconCompat.CreateWithResource(context!, ResourceConstant.Drawable.exo_icon_circular_play);
            var playAction = new NotificationCompat.Action(
                playIcon,
                "Play",
                playIntent);

            builder.AddAction(playAction);
        }

        // Attach MediaSession for Android Auto integration
        if (mediaSession?.SessionToken != null)
        {
            var mediaStyle = new MediaStyle()
                .SetMediaSession(mediaSession!.SessionToken)
                .SetShowActionsInCompactView(0); // Show first action (play) in compact view
            
            builder.SetStyle(mediaStyle);
        }

        // Add artwork if available
        if (artwork != null)
        {
            builder.SetLargeIcon(artwork);
        }

        return builder.Build() ?? throw new InvalidOperationException("Failed to build notification");
    }
}
