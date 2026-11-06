#nullable enable

// Java binding parameter names are not valid C# identifiers
#pragma warning disable S927

using _Microsoft.Android.Resource.Designer;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using AndroidX.Media3.Session;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Platforms.Android.Services.AndroidAuto.Interfaces;
using Bible.Alarm.Platforms.Android.Services.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Playback;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Media3;

/// <summary>
/// Single Media3 library service for in-app playback and Android Auto.
/// </summary>
[IntentFilter(new[] { "androidx.media3.session.MediaLibraryService", "android.media.browse.MediaBrowserService" })]
[Service(
    Exported = true,
    ForegroundServiceType = ForegroundService.TypeMediaPlayback,
    Name = "bible.alarm.platforms.android.services.media3.BibleAlarmMediaLibraryService")]
[Register("bible.alarm.platforms.android.services.media3.BibleAlarmMediaLibraryService")]
#pragma warning disable CS0618 // Xamarin Preserve is still required for JNI Media3 callbacks
[Preserve(AllMembers = true)]
#pragma warning restore CS0618
public class BibleAlarmMediaLibraryService : MediaLibraryService
{
    private static readonly ILogger logger = Log.ForContext<BibleAlarmMediaLibraryService>();
    private const int LibraryForegroundNotificationId = 3;
    private static BibleAlarmMediaLibraryService? current;

    public static bool IsForeground { get; private set; }

    private MediaLibraryService.MediaLibrarySession? session;
    private readonly Media3StateSubscription subscription = new(logger);
    private readonly ForegroundStartListener foregroundListener = new();

    public static void ApplySessionActive(bool active)
    {
        var service = current;
        if (service == null)
        {
            return;
        }

        service.SetShowNotificationForIdlePlayer(active
            ? AndroidX.Media3.Session.MediaSessionService.ShowNotificationForIdlePlayerAlways
            : AndroidX.Media3.Session.MediaSessionService.ShowNotificationForIdlePlayerNever);
    }

    public override void OnCreate()
    {
        SetCurrent(this);
        ForegroundServiceOperations.StartForegroundMinimal(this, LibraryForegroundNotificationId);
        SetIsForeground(true);
        ReleaseAlarmIfPlaybackRunning();
        _ = SessionStatePlayer.Instance;
        base.OnCreate();

        try
        {
            // Same id and channel as the startup placeholder, so Media3 replaces
            // "Starting..." in place instead of posting a second notification.
            var provider = new DefaultMediaNotificationProvider.Builder(this)
                .SetNotificationId(LibraryForegroundNotificationId)!
                .SetChannelId(ForegroundNotificationHelper.ForegroundChannelId)!
                .Build()!;
            provider.SetSmallIcon(ResourceConstant.Drawable.exo_icon_circular_play);
            SetMediaNotificationProvider(provider);
            SetListener(foregroundListener);
            var callback = new LibraryCallback();
            session = new MediaLibraryService.MediaLibrarySession.Builder(this, SessionStatePlayer.Instance, callback)
                .SetId("BibleAlarmSession")!
                .Build();
            // The platform sees a MediaSession as soon as it is built. The service
            // posts its media notification only for sessions passed to AddSession.
            AddSession(session);
            ApplySessionActive(SessionStatePlayer.Instance.IsSessionActive);
            subscription.SetSession(session!);
            _ = subscription.InitializeAsync();
            StartBootstrap();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Media3 library service failed during OnCreate");
        }
    }

    public override MediaLibraryService.MediaLibrarySession? OnGetSessionFromMediaLibraryService(MediaSession.ControllerInfo? controllerInfo)
    {
        _ = controllerInfo;
        return session;
    }

    public override MediaSession? OnGetSession(MediaSession.ControllerInfo? controllerInfo)
    {
        _ = controllerInfo;
        return session;
    }

    public override void OnTaskRemoved(Intent? rootIntent)
    {
        base.OnTaskRemoved(rootIntent);
        var notifications = GetSystemService(NotificationService) as NotificationManager;
        notifications?.CancelAll();
        _ = StopPlaybackAsync();
    }

    public override void OnDestroy()
    {
        subscription.Cleanup();
        session?.Release();
        session = null;
        if (current == this)
        {
            SetCurrent(null);
            SetIsForeground(false);
        }

        base.OnDestroy();
    }

    private void StartBootstrap()
    {
        _ = Task.Run(() =>
        {
            try
            {
                MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);
                var rotation = ServiceProviderManager.GetService<IAndroidAutoDefaultScheduleRotationService>();
                rotation?.Start();
                RefreshDefaultMetadataAfterBootstrap();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Media3 library service bootstrap failed");
            }
        });
    }

    private void RefreshDefaultMetadataAfterBootstrap()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await MauiProgram.WaitForBootstrapAsync();
                var playback = ServiceProviderManager.GetService<IState<PlaybackState>>();
                if (playback?.Value?.IsPreparingOrPlaying is true)
                {
                    return;
                }

                ServiceProviderManager.GetService<Fluxor.IDispatcher>()?.Dispatch(new SetCarPlayScreenAction());
                var childCount = AndroidAutoScheduleHelperCount();
                session?.NotifyChildrenChanged(Media3LibraryCatalog.RootId, childCount, null);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.FailedToRefreshDefaultMetadataOnCarConnect);
            }
        });
    }

    private static int AndroidAutoScheduleHelperCount()
    {
        return Platforms.Android.Services.AndroidAuto.AndroidAutoScheduleHelper.LoadScheduleStateItemsFromState().Count;
    }

    private static async Task StopPlaybackAsync()
    {
        try
        {
            var player = ServiceProviderManager.GetService<IAudioPlayer>();
            if (player != null)
            {
                await player.StopAsync();
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Swipe-away stop failed");
        }
    }

    /// <summary>
    /// Drops the alarm foreground service once this service is in the foreground and playback is running.
    /// </summary>
    public static void ReleaseAlarmIfPlaybackRunning()
    {
        if (!IsForeground)
        {
            return;
        }

        var playback = ServiceProviderManager.GetService<IState<PlaybackState>>();
        if (playback?.Value?.IsPreparingOrPlaying == true)
        {
            ForegroundServiceCoordinator.StopAlarmForegroundServiceIfActive();
        }
    }

    private static void SetCurrent(BibleAlarmMediaLibraryService? service) => current = service;

    private static void SetIsForeground(bool value) => IsForeground = value;

    private sealed class ForegroundStartListener : Java.Lang.Object, MediaSessionService.IListener
    {
        public void OnForegroundServiceStartNotAllowedException()
        {
            SetIsForeground(false);
            logger.Warning("Media3 foreground start was not allowed. The alarm foreground service stays up.");
            PostTapToResumeNotification();
        }

        private static void PostTapToResumeNotification()
        {
            var service = current;
            if (service == null)
            {
                return;
            }

            try
            {
                var notification = ForegroundNotificationHelper.CreateNotification(service, isAlarmNotification: false);
                var manager = service.GetSystemService(NotificationService) as NotificationManager;
                manager?.Notify(LibraryForegroundNotificationId, notification);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Could not post the tap-to-resume notification");
            }
        }
    }
}

#pragma warning restore S927
