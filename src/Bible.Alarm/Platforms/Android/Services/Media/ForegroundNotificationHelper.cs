#nullable enable

using _Microsoft.Android.Resource.Designer;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using AndroidX.Core.Content;
using AndroidX.Core.Graphics.Drawable;
using Serilog;
using Application = Android.App.Application;
using MediaStyle = AndroidX.Media.App.NotificationCompat.MediaStyle;
using NotificationCompat = AndroidX.Core.App.NotificationCompat;

namespace Bible.Alarm.Platforms.Android.Services.Media;

/// <summary>
/// Handles foreground service notification creation and channel management.
/// Used by both Android Auto and Alarm foreground services.
/// </summary>
internal static class ForegroundNotificationHelper
{
    private static readonly ILogger logger = Log.ForContext(typeof(ForegroundNotificationHelper));
    private const int ForegroundNotificationId = 2; // MediaElement uses 1, so we use 2
    private const string ForegroundChannelId = "foreground_service_channel";
    private const string ForegroundChannelName = "Media Playback";

    public static int NotificationId => ForegroundNotificationId;

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
                        ForegroundChannelId,
                        ForegroundChannelName,
                        NotificationImportance.Low); // Low priority when idle

                    channel.Description = "Notifications shown during Bible reading and music playback";
                    channel.SetShowBadge(false);
                    channel.EnableLights(false);
                    channel.EnableVibration(false);

                    // Explicitly set no sound for foreground service notifications (silent)
                    // This ensures alarm foreground notifications don't play sound when tap is disabled
                    channel.SetSound(null, null);

                    notificationManager.CreateNotificationChannel(channel);
                    logger.Debug("Created notification channel: {ChannelId}", ForegroundChannelId);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error creating notification channel (may already exist)");
        }
    }

    /// <summary>
    /// Creates a bare-minimum fallback notification when CreateMinimalNotification fails.
    /// Uses framework resources where possible to avoid dependency on app resources.
    /// </summary>
    public static Notification CreateFallbackNotification(Service service)
    {
        var context = service?.ApplicationContext ?? Application.Context
            ?? throw new InvalidOperationException("Context cannot be null");

        try
        {
            if (service != null)
            {
                CreateNotificationChannel(service);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "CreateFallbackNotification: channel creation failed");
        }

        var builder = new NotificationCompat.Builder(context, ForegroundChannelId);
        builder.SetSmallIcon(ResourceConstant.Drawable.ic_launcher_round);
        builder.SetContentTitle("Bible Alarm");
        builder.SetContentText("Loading...");
        builder.SetOngoing(true);
        builder.SetForegroundServiceBehavior(NotificationCompat.ForegroundServiceImmediate);
        builder.SetPriority(NotificationCompat.PriorityMin);

        return builder.Build() ?? throw new InvalidOperationException("Failed to build fallback notification");
    }

    /// <summary>
    /// Creates a minimal notification for immediate foreground service start.
    /// No MediaSession, no PendingIntents, no artwork — just enough to satisfy Android's
    /// startForeground() requirement and keep the process alive.
    /// Uses app launcher icon so the small icon always shows (avoids "no icon" duplicate on lock screen).
    /// </summary>
    public static Notification CreateMinimalNotification(Service service)
    {
        var context = service.ApplicationContext ?? Application.Context
            ?? throw new InvalidOperationException("Context cannot be null");

        var builder = new NotificationCompat.Builder(context, ForegroundChannelId);
        builder.SetSmallIcon(ResourceConstant.Drawable.ic_launcher_round);
        builder.SetContentTitle("Bible Alarm");
        builder.SetContentText("Starting...");
        builder.SetOngoing(true);
        builder.SetForegroundServiceBehavior(NotificationCompat.ForegroundServiceImmediate);
        builder.SetVisibility(NotificationCompat.VisibilityPublic);
        builder.SetPriority(NotificationCompat.PriorityLow);
        builder.SetShowWhen(false);
        builder.SetOnlyAlertOnce(true);
        builder.SetSilent(true);

        return builder.Build() ?? throw new InvalidOperationException("Failed to build minimal notification");
    }

    public static Notification CreateNotification(Service service, MediaSessionCompat mediaSession, bool isAlarmNotification = false)
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

        string title;
        string artist;
        Bitmap? artwork = null;

        if (isAlarmNotification)
        {
            // Alarm notification: Show app icon and "preparing" message
            // Alarm automatically starts playback, so we show a preparing state
            title = "Bible Alarm";
            artist = "Preparing playback...";
            artwork = null; // Don't use artwork, use app icon instead
        }
        else
        {
            // Android Auto notification: Use metadata from MediaSession or fallbacks
            // Android Auto requires user to start playback, so we show available metadata
            var metadata = mediaSession?.Controller?.Metadata;
            title = metadata?.GetString(MediaMetadataCompat.MetadataKeyTitle) ?? string.Empty;
            artist = metadata?.GetString(MediaMetadataCompat.MetadataKeyArtist) ?? string.Empty;
            artwork = metadata?.GetBitmap(MediaMetadataCompat.MetadataKeyArt);

            // Fallback values for early bootstrap scenarios (before metadata is set)
            if (string.IsNullOrEmpty(title))
            {
                title = "Bible Alarm";
            }
            if (string.IsNullOrEmpty(artist))
            {
                artist = "Ready to play";
            }
        }

        // Create notification builder - context is guaranteed non-null at this point
        // Split the fluent API chain to avoid compiler warnings
        var builder = new NotificationCompat.Builder(context!, ForegroundChannelId);
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

        // Explicitly disable sound for foreground service notifications (silent)
        // For Android O+, channel sound settings apply, but for pre-O we need to set it here
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            builder.SetSound(null);
            builder.SetDefaults(0); // No default sounds, lights, or vibrations
        }

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
        if (mediaSession?.SessionToken is { } sessionToken)
        {
            var mediaStyle = new MediaStyle()
                .SetMediaSession(sessionToken)!
                .SetShowActionsInCompactView(0); // Show first action (play) in compact view

            // MediaStyle fluent API always returns non-null, but compiler doesn't know this
            builder.SetStyle(mediaStyle!);
        }

        // Add artwork or app icon
        if (isAlarmNotification)
        {
            // For alarm, use app icon as large icon
            var appIcon = GetAppIcon(context);
            if (appIcon != null)
            {
                builder.SetLargeIcon(appIcon);
            }
        }
        else if (artwork != null)
        {
            // For Android Auto, use artwork if available
            builder.SetLargeIcon(artwork);
        }

        return builder.Build() ?? throw new InvalidOperationException("Failed to build notification");
    }

    /// <summary>
    /// Gets the app icon as a Bitmap for use in notifications.
    /// </summary>
    private static Bitmap? GetAppIcon(Context context)
    {
        try
        {
            var drawable = ContextCompat.GetDrawable(context, ResourceConstant.Drawable.ic_launcher_round);
            if (drawable != null)
            {
                return DrawableToBitmap(drawable);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to get app icon for alarm notification");
        }
        return null;
    }

    /// <summary>
    /// Converts a Drawable to a Bitmap for use in notifications.
    /// </summary>
    private static Bitmap DrawableToBitmap(Drawable drawable)
    {
        if (drawable is BitmapDrawable bitmapDrawable && bitmapDrawable.Bitmap != null)
        {
            return bitmapDrawable.Bitmap;
        }

        Bitmap bitmap;
        if (drawable.IntrinsicWidth <= 0 || drawable.IntrinsicHeight <= 0)
        {
            // Single color bitmap will be created of 1x1 pixel
            bitmap = Bitmap.CreateBitmap(1, 1, Bitmap.Config.Argb8888!);
        }
        else
        {
            bitmap = Bitmap.CreateBitmap(drawable.IntrinsicWidth, drawable.IntrinsicHeight, Bitmap.Config.Argb8888!);
        }

        var canvas = new Canvas(bitmap);
        drawable.SetBounds(0, 0, canvas.Width, canvas.Height);
        drawable.Draw(canvas);
        return bitmap;
    }
}
