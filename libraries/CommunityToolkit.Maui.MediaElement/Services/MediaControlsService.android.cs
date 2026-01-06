using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Media3.Session;
using AndroidX.Media3.UI;
using CommunityToolkit.Maui.Services;

namespace CommunityToolkit.Maui.Media.Services;

[SupportedOSPlatform("Android26.0")]
[IntentFilter(["androidx.media3.session.MediaSessionService"])]
[Service(Exported = false, Enabled = true, Name = "communityToolkit.maui.media.services", ForegroundServiceType = ForegroundService.TypeMediaPlayback)]
sealed class MediaControlsService : Service
{
    readonly WeakEventManager taskRemovedEventManager = new();

    bool isDisposed;

    PlayerNotificationManager? playerNotificationManager;
    NotificationCompat.Builder? notificationBuilder;

    public event EventHandler TaskRemoved
    {
        add => taskRemovedEventManager.AddEventHandler(value);
        remove => taskRemovedEventManager.RemoveEventHandler(value);
    }

    public BoundServiceBinder? Binder { get; private set; }
    public NotificationManager? NotificationManager { get; private set; }

    public override IBinder? OnBind(Intent? intent)
    {
        Binder = new BoundServiceBinder(this);
        return Binder;
    }

    public override void OnCreate()
    {
        base.OnCreate();
        StartForegroundServices();
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        return StartCommandResult.NotSticky;
    }

    public override void OnTaskRemoved(Intent? rootIntent)
    {
        base.OnTaskRemoved(rootIntent);
        taskRemovedEventManager.HandleEvent(this, EventArgs.Empty, nameof(TaskRemoved));

        playerNotificationManager?.SetPlayer(null);
        NotificationManager?.CancelAll();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        playerNotificationManager?.SetPlayer(null);
        NotificationManager?.CancelAll();
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            StopForeground(true);
        }

        StopSelf();
    }

    public override void OnRebind(Intent? intent)
    {
        base.OnRebind(intent);
        StartForegroundServices();
    }

    [MemberNotNull(nameof(NotificationManager), nameof(notificationBuilder))]
    public void UpdateNotifications(in MediaSession session, in PlatformMediaElement mediaElement)
    {
        ArgumentNullException.ThrowIfNull(notificationBuilder);
        ArgumentNullException.ThrowIfNull(NotificationManager);

        // Ensure notification channel exists and is configured as silent
        CreateNotificationChannel(NotificationManager);

        var style = new MediaStyleNotificationHelper.MediaStyle(session);
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            SetLegacyNotifications(session, mediaElement);
        }

        notificationBuilder.SetStyle(style);

        // For pre-O Android, explicitly ensure no sound
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            notificationBuilder.SetSound(null);
            notificationBuilder.SetDefaults(0);
        }

        NotificationManagerCompat.From(Platform.AppContext)?.Notify(1, notificationBuilder.Build());
    }

    static PendingIntent CreateActivityPendingIntent()
    {
        var packageName = Platform.AppContext.PackageName ?? throw new InvalidOperationException("PackageName cannot be null");
        var packageManager = Platform.AppContext.PackageManager ?? throw new InvalidOperationException("PackageManager cannot be null");
        var launchIntent = packageManager.GetLaunchIntentForPackage(packageName) ?? throw new InvalidOperationException("Launch intent cannot be null");

        launchIntent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);

        var flags = PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable;
        return PendingIntent.GetActivity(Platform.AppContext, 0, launchIntent, flags)
               ?? throw new InvalidOperationException("PendingIntent cannot be null");
    }

    [MemberNotNull(nameof(playerNotificationManager))]
    public void SetLegacyNotifications(in MediaSession session, in PlatformMediaElement mediaElement)
    {
        ArgumentNullException.ThrowIfNull(session);
        playerNotificationManager ??= new PlayerNotificationManager.Builder(Platform.AppContext, 1, "1").Build()
                                      ?? throw new InvalidOperationException("PlayerNotificationManager cannot be null");

        playerNotificationManager.SetUseFastForwardAction(true);
        playerNotificationManager.SetUseFastForwardActionInCompactView(true);
        playerNotificationManager.SetUseRewindAction(true);
        playerNotificationManager.SetUseRewindActionInCompactView(true);
        playerNotificationManager.SetUseNextAction(true);
        playerNotificationManager.SetUseNextActionInCompactView(true);
        playerNotificationManager.SetUsePlayPauseActions(true);
        playerNotificationManager.SetUsePreviousAction(true);
        playerNotificationManager.SetColor(_Microsoft.Android.Resource.Designer.Resource.Color.abc_primary_text_material_dark);
        playerNotificationManager.SetUsePreviousActionInCompactView(true);
        playerNotificationManager.SetVisibility(NotificationCompat.VisibilityPublic);
        playerNotificationManager.SetMediaSessionToken(session.PlatformToken);
        playerNotificationManager.SetPlayer(mediaElement);
        playerNotificationManager.SetColorized(true);
        playerNotificationManager.SetShowPlayButtonIfPlaybackIsSuppressed(true);
        playerNotificationManager.SetSmallIcon(_Microsoft.Android.Resource.Designer.Resource.Drawable.media3_notification_small_icon);
        playerNotificationManager.SetPriority(NotificationCompat.PriorityDefault);
        playerNotificationManager.SetUseChronometer(true);
    }

    protected override void Dispose(bool disposing)
    {
        if (!isDisposed)
        {
            if (disposing)
            {
                NotificationManager?.Dispose();
                NotificationManager = null;

                playerNotificationManager?.Dispose();
                playerNotificationManager = null;

                if (!OperatingSystem.IsAndroidVersionAtLeast(33))
                {
                    StopForeground(true);
                }

                StopSelf();
            }

            isDisposed = true;
        }

        base.Dispose(disposing);
    }

    static void CreateNotificationChannel(in NotificationManager notificationMnaManager)
    {
        var channel = new NotificationChannel("1", "Now Playing", NotificationImportance.Low);
        channel.Description = "Shows currently playing Bible reading or music";
        // Explicitly set no sound for MediaElement notifications (silent)
        // User-initiated playback should not make notification sounds
        channel.SetSound(null, null);
        notificationMnaManager.CreateNotificationChannel(channel);
    }

    [MemberNotNull(nameof(notificationBuilder), nameof(NotificationManager))]
    void StartForegroundServices()
    {
        NotificationManager ??= GetSystemService(NotificationService) as NotificationManager ?? throw new InvalidOperationException($"{nameof(NotificationManager)} cannot be null");
        
        // Create notification channel BEFORE creating the builder to ensure it's available
        CreateNotificationChannel(NotificationManager);
        
        notificationBuilder ??= new NotificationCompat.Builder(Platform.AppContext, "1");

        var pendingIntent = CreateActivityPendingIntent();
        notificationBuilder.SetSmallIcon(_Microsoft.Android.Resource.Designer.Resource.Drawable.media3_notification_small_icon);
        notificationBuilder.SetAutoCancel(false);
        notificationBuilder.SetForegroundServiceBehavior(NotificationCompat.ForegroundServiceImmediate);
        notificationBuilder.SetVisibility(NotificationCompat.VisibilityPublic);
        notificationBuilder.SetContentIntent(pendingIntent);

        // Explicitly disable sound for MediaElement notifications (silent)
        // For pre-O Android, we need to set it in the builder as well
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            notificationBuilder.SetSound(null);
            notificationBuilder.SetDefaults(0); // No default sounds, lights, or vibrations
        }
        // For Android O+, the channel's sound setting (null) is automatically used

        // ForegroundService.TypeMediaPlayback requires API 29+, minimum supported is API 26
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            if (notificationBuilder.Build() is Notification notification)
            {
                StartForeground(1, notification, ForegroundService.TypeMediaPlayback);
            }
        }
        else
        {
            StartForeground(1, notificationBuilder.Build());
        }
    }
}