#nullable enable
using _Microsoft.Android.Resource.Designer;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using AndroidX.Core.App;
using AndroidX.Media.App;
using Serilog;
using Application = Android.App.Application;
using NotificationCompat = AndroidX.Core.App.NotificationCompat;
using MediaStyle = AndroidX.Media.App.NotificationCompat.MediaStyle;
using Android.Content.PM;
using AndroidX.Media.Session;

namespace Bible.Alarm.Platforms.Android.Services.Media;

/// <summary>
/// Coordinates foreground service ownership between MediaElement playback and Android Auto.
/// Android only allows one foreground service at a time, so this ensures proper handoff.
/// Only starts Android Auto foreground service when Android Auto is actually connected.
/// </summary>
public sealed class ForegroundServiceCoordinator
{
    private static readonly ILogger logger = Log.ForContext<ForegroundServiceCoordinator>();
    private static readonly Lock @lock = new();
    
    /// <summary>
    /// Represents the current owner of the foreground service.
    /// </summary>
    public enum ForegroundServiceOwner
    {
        None,
        MediaElement,      // MediaControlsService from MediaElement library (uses notification ID 1)
        AndroidAuto        // Android Auto connection service (uses notification ID 2)
    }
    
    private static ForegroundServiceOwner currentOwner = ForegroundServiceOwner.None;
    private static Service? androidAutoForegroundService;
    private const int AndroidAutoNotificationId = 2; // MediaElement uses 1, so we use 2
    private const string AndroidAutoChannelId = "android_auto_channel";
    private const string AndroidAutoChannelName = "Android Auto Connection";
    private static bool isMediaElementPlaying = false;
    private static bool isAndroidAutoConnected = false; // Track connection state

    /// <summary>
    /// Called when Android Auto connects (OnGetRoot or OnBind for LegacyMediaBrowserService, 
    /// or OnCreateSession for CarAppService).
    /// Only starts foreground service if MediaElement is not active.
    /// </summary>
    /// <param name="service">The Android Auto service instance</param>
    public static void OnAndroidAutoConnected(Service service)
    {
        lock (@lock)
        {
            if (isAndroidAutoConnected)
            {
                logger.Debug("Android Auto already marked as connected");
                // Update service reference in case it changed
                androidAutoForegroundService = service;
                return;
            }

            isAndroidAutoConnected = true;
            androidAutoForegroundService = service; // Store service instance so it can be used when metadata is set later
            logger.Information("Android Auto connected - will start foreground service if metadata is available and MediaElement is not active");

            // Check if MediaElement is active - if so, don't start Android Auto foreground service
            if (isMediaElementPlaying || currentOwner == ForegroundServiceOwner.MediaElement)
            {
                logger.Information("Android Auto connected but MediaElement is active - will start foreground service when MediaElement stops");
                return;
            }

            // If metadata is already available, start foreground service immediately
            var session = MediaSessionHelper.Create();
            if (session?.Controller?.Metadata != null)
            {
                var hasMetadata = !string.IsNullOrEmpty(
                    session.Controller.Metadata.GetString(MediaMetadataCompat.MetadataKeyTitle));
                
                if (hasMetadata)
                {
                    logger.Information("Metadata available - starting Android Auto foreground service immediately");
                    RequestForAndroidAuto(service, session);
                }
                else
                {
                    logger.Debug("Android Auto connected but no metadata yet - will start foreground when metadata is set");
                }
            }
            else
            {
                logger.Debug("Android Auto connected but MediaSession not ready - will start foreground when metadata is set");
            }
        }
    }

    /// <summary>
    /// Called when Android Auto disconnects (OnUnbind for LegacyMediaBrowserService).
    /// Only stops the foreground service if Android Auto is the current owner.
    /// MediaElement manages its own foreground service lifecycle, so we don't interfere with it.
    /// </summary>
    public static void OnAndroidAutoDisconnected()
    {
        lock (@lock)
        {
            if (!isAndroidAutoConnected)
            {
                logger.Debug("Android Auto already marked as disconnected");
                return;
            }

            isAndroidAutoConnected = false;
            logger.Information("Android Auto disconnected");

            // Only stop Android Auto foreground service if it's the current owner
            // MediaElement manages its own foreground service, so we don't touch it
            if (currentOwner == ForegroundServiceOwner.AndroidAuto)
            {
                logger.Information("Stopping Android Auto foreground service and clearing service reference");
                StopAndroidAutoForeground();
                currentOwner = ForegroundServiceOwner.None;
            }
            else
            {
                logger.Debug("Android Auto disconnected but was not the foreground service owner (current owner: {CurrentOwner}) - not stopping foreground service", currentOwner);
            }
            
            // Clear the service reference when Android Auto actually disconnects
            androidAutoForegroundService = null;
        }
    }

    /// <summary>
    /// Called when MediaElement starts playing (detected via playback state changes).
    /// If Android Auto is currently foreground, it will be stopped first to ensure smooth transition.
    /// </summary>
    public static void OnPlaybackStarted()
    {
        lock (@lock)
        {
            isMediaElementPlaying = true;
            
            if (currentOwner == ForegroundServiceOwner.MediaElement)
            {
                logger.Debug("MediaElement already owns foreground service");
                return;
            }

            logger.Information("Playback started - MediaElement requesting foreground service ownership (current owner: {CurrentOwner})", currentOwner);

            // Stop Android Auto foreground if active
            if (currentOwner == ForegroundServiceOwner.AndroidAuto)
            {
                logger.Information("Stopping Android Auto foreground service before MediaElement starts");
                StopAndroidAutoForeground();
                // Small delay to ensure Android Auto foreground service is fully stopped
                // and notification is removed before MediaElement starts its own
                // Note: This is a brief synchronous delay - MediaElement's service will start shortly after
                System.Threading.Thread.Sleep(100);
                logger.Information("Android Auto foreground stopped - MediaElement can now start");
            }

            // MediaElement's MediaControlsService will start itself via MediaManager.StartService()
            // We just mark ownership - don't start service here
            currentOwner = ForegroundServiceOwner.MediaElement;
            logger.Information("Foreground service ownership transferred to MediaElement");
        }
    }

    /// <summary>
    /// Called when MediaElement stops playing (detected via playback state changes).
    /// Releases ownership but doesn't immediately start Android Auto foreground service.
    /// Android Auto foreground service should be started after MediaElement is fully disposed
    /// and metadata is set (via HandleSetDefaultScheduleMetadata).
    /// </summary>
    public static void OnPlaybackStopped()
    {
        lock (@lock)
        {
            isMediaElementPlaying = false;
            
            if (currentOwner != ForegroundServiceOwner.MediaElement)
            {
                logger.Debug("MediaElement does not own foreground service (current owner: {CurrentOwner})", currentOwner);
                return;
            }

            logger.Information("Playback stopped - MediaElement releasing foreground service ownership (will wait for disposal before starting Android Auto foreground)");
            currentOwner = ForegroundServiceOwner.None;
            
            // Don't start Android Auto foreground service here - wait for MediaElement to be fully disposed
            // and metadata to be set. This ensures MediaElement's notification is removed first.
        }
    }

    /// <summary>
    /// Called when MediaElement is fully disposed and its notification/foreground service is removed.
    /// This ensures proper synchronization - MediaElement's notification is removed before
    /// Android Auto foreground service starts.
    /// </summary>
    public static void OnMediaElementDisposed()
    {
        lock (@lock)
        {
            // If MediaElement was the owner, ensure it's released
            if (currentOwner == ForegroundServiceOwner.MediaElement)
            {
                logger.Information("MediaElement disposed - releasing foreground service ownership");
                currentOwner = ForegroundServiceOwner.None;
            }
            
            // MediaElement's notification should now be removed
            // Android Auto can take ownership if it's connected and metadata is available
            // This will be triggered by HandleSetDefaultScheduleMetadata after metadata is set
            logger.Debug("MediaElement disposal complete - Android Auto can now take ownership when metadata is set");
        }
    }

    /// <summary>
    /// Requests foreground service ownership for Android Auto.
    /// Only succeeds if Android Auto is connected and MediaElement is not currently playing.
    /// Should be called whenever default schedule metadata is set.
    /// Uses the stored service instance from when Android Auto connected.
    /// </summary>
    /// <param name="mediaSession">The MediaSessionCompat to attach to notification</param>
    /// <returns>True if ownership was granted or already owned, false if Android Auto not connected or MediaElement is active</returns>
    public static bool RequestForAndroidAuto(MediaSessionCompat mediaSession)
    {
        lock (@lock)
        {
            if (androidAutoForegroundService == null)
            {
                logger.Debug("Cannot start Android Auto foreground service - no service instance available (Android Auto may not be connected yet)");
                return false;
            }
            
            return RequestForAndroidAuto(androidAutoForegroundService, mediaSession);
        }
    }

    /// <summary>
    /// Requests foreground service ownership for Android Auto.
    /// Only succeeds if Android Auto is connected and MediaElement is not currently playing.
    /// Should be called whenever default schedule metadata is set.
    /// </summary>
    /// <param name="service">The Android Auto service instance (LegacyMediaBrowserService)</param>
    /// <param name="mediaSession">The MediaSessionCompat to attach to notification</param>
    /// <returns>True if ownership was granted or already owned, false if Android Auto not connected or MediaElement is active</returns>
    public static bool RequestForAndroidAuto(Service service, MediaSessionCompat mediaSession)
    {
        lock (@lock)
        {
            // Only start foreground if Android Auto is actually connected
            if (!isAndroidAutoConnected)
            {
                logger.Debug("Cannot start Android Auto foreground service - Android Auto is not connected");
                return false;
            }

            // If MediaElement is playing, don't start Android Auto foreground
            if (isMediaElementPlaying || currentOwner == ForegroundServiceOwner.MediaElement)
            {
                logger.Debug("Cannot start Android Auto foreground service - MediaElement is playing (isPlaying: {IsPlaying}, owner: {Owner})", 
                    isMediaElementPlaying, currentOwner);
                return false;
            }

            if (currentOwner == ForegroundServiceOwner.AndroidAuto)
            {
                logger.Debug("Android Auto already owns foreground service - updating notification");
                UpdateAndroidAutoForeground(service, mediaSession);
                return true;
            }

            logger.Information("Android Auto requesting foreground service ownership (Android Auto is connected)");
            StartAndroidAutoForeground(service, mediaSession);
            currentOwner = ForegroundServiceOwner.AndroidAuto;
            logger.Information("Foreground service ownership granted to Android Auto");
            return true;
        }
    }

    /// <summary>
    /// Gets the current foreground service owner.
    /// </summary>
    public static ForegroundServiceOwner CurrentOwner => currentOwner;

    /// <summary>
    /// Gets whether Android Auto is currently connected.
    /// </summary>
    public static bool IsAndroidAutoConnected => isAndroidAutoConnected;

    private static void StartAndroidAutoForeground(Service service, MediaSessionCompat mediaSession)
    {
        try
        {
            androidAutoForegroundService = service;
            
            // Ensure notification channel exists
            CreateNotificationChannel(service);
            
            // Create notification with MediaSession attached
            var notification = CreateAndroidAutoNotification(service, mediaSession);
            
            // Start foreground service with sticky notification
            // Use ForegroundService.TypeMediaPlayback to match MediaElement's type
            if (OperatingSystem.IsAndroidVersionAtLeast(29))
            {
                service.StartForeground(AndroidAutoNotificationId, notification, ForegroundService.TypeMediaPlayback);
            }
            else
            {
                service.StartForeground(AndroidAutoNotificationId, notification);
            }
            
            logger.Information("Android Auto foreground service started with notification ID {NotificationId}", AndroidAutoNotificationId);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error starting Android Auto foreground service");
        }
    }

    private static void UpdateAndroidAutoForeground(Service service, MediaSessionCompat mediaSession)
    {
        try
        {
            var notification = CreateAndroidAutoNotification(service, mediaSession);
            var notificationManager = NotificationManagerCompat.From(service);
            notificationManager?.Notify(AndroidAutoNotificationId, notification);
            logger.Debug("Android Auto foreground notification updated (ID: {NotificationId})", AndroidAutoNotificationId);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error updating Android Auto foreground notification");
        }
    }

    private static void StopAndroidAutoForeground()
    {
        try
        {
            if (androidAutoForegroundService != null)
            {
                // Stop foreground service - this removes the notification
                if (OperatingSystem.IsAndroidVersionAtLeast(33))
                {
                    androidAutoForegroundService.StopForeground(StopForegroundFlags.Remove);
                }
                else
                {
                    androidAutoForegroundService.StopForeground(true);
                }
                
                // Explicitly cancel the notification to ensure it disappears
                // This prevents having two notifications (MediaElement ID 1 and Android Auto ID 2) at the same time
                var notificationManager = NotificationManagerCompat.From(androidAutoForegroundService);
                notificationManager?.Cancel(AndroidAutoNotificationId);
                
                logger.Information("Android Auto foreground service stopped and notification cancelled (ID: {NotificationId})", AndroidAutoNotificationId);
            }
            // DO NOT clear androidAutoForegroundService here - keep it as long as Android Auto is connected
            // It will be cleared in OnAndroidAutoDisconnected() when Android Auto actually disconnects
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error stopping Android Auto foreground service");
        }
    }

    private static void CreateNotificationChannel(Service service)
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

    private static Notification CreateAndroidAutoNotification(Service service, MediaSessionCompat mediaSession)
    {
        var context = service.ApplicationContext ?? Application.Context;
        
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
        var metadata = mediaSession.Controller?.Metadata;
        var title = metadata?.GetString(MediaMetadataCompat.MetadataKeyTitle) ?? "Bible Alarm";
        var artist = metadata?.GetString(MediaMetadataCompat.MetadataKeyArtist) ?? "Ready to play";
        var artwork = metadata?.GetBitmap(MediaMetadataCompat.MetadataKeyArt);

        var builder = new NotificationCompat.Builder(context, AndroidAutoChannelId)
            .SetSmallIcon(ResourceConstant.Drawable.exo_icon_circular_play)
            .SetContentTitle(title)
            .SetContentText(artist)
            .SetContentIntent(pendingIntent)
            .SetAutoCancel(false) // Sticky notification
            .SetOngoing(true) // Makes it sticky/ongoing
            .SetForegroundServiceBehavior(NotificationCompat.ForegroundServiceImmediate)
            .SetVisibility(NotificationCompat.VisibilityPublic)
            .SetPriority(NotificationCompat.PriorityLow) // Low priority when idle
            .SetShowWhen(false)
            .SetOnlyAlertOnce(true);

        // Create play action button that triggers MediaSession.OnPlay()
        // This allows users to tap play on the notification, just like on Android Auto screen
        var playIntent = MediaButtonReceiver.BuildMediaButtonPendingIntent(
            context,
            PlaybackStateCompat.ActionPlay);
        
        var playAction = new NotificationCompat.Action(
            ResourceConstant.Drawable.exo_icon_circular_play,
            "Play",
            playIntent);

        // Add play action to notification
        builder.AddAction(playAction);

        // Attach MediaSession for Android Auto integration
        var mediaStyle = new MediaStyle()
            .SetMediaSession(mediaSession.SessionToken)
            .SetShowActionsInCompactView(0); // Show first action (play) in compact view
        
        builder.SetStyle(mediaStyle);

        // Add artwork if available
        if (artwork != null)
        {
            builder.SetLargeIcon(artwork);
        }

        return builder.Build();
    }
}
