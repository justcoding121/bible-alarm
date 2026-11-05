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
/// Inactive until cutover: it has no intent filter and is not registered in DI.
/// </summary>
[Service(
    Exported = true,
    ForegroundServiceType = ForegroundService.TypeMediaPlayback,
    Name = "bible.alarm.platforms.android.services.media3.BibleAlarmMediaLibraryService")]
[Register("bible.alarm.platforms.android.services.media3.BibleAlarmMediaLibraryService")]
[Preserve(AllMembers = true)]
public class BibleAlarmMediaLibraryService : MediaLibraryService
{
    private static readonly ILogger logger = Log.ForContext<BibleAlarmMediaLibraryService>();
    private static BibleAlarmMediaLibraryService? current;

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
        current = this;
        ForegroundServiceOperations.StartForegroundMinimal(this);
        _ = SessionStatePlayer.Instance;
        base.OnCreate();

        try
        {
            var callback = new LibraryCallback();
            session = new MediaLibraryService.MediaLibrarySession.Builder(this, SessionStatePlayer.Instance, callback)
                .SetId("BibleAlarmSession")!
                .Build();
            var provider = new DefaultMediaNotificationProvider(this);
            provider.SetSmallIcon(ResourceConstant.Drawable.exo_icon_circular_play);
            SetMediaNotificationProvider(provider);
            SetListener(foregroundListener);
            ApplySessionActive(SessionStatePlayer.Instance.IsSessionActive);
            subscription.SetSession(session!);
            _ = subscription.InitializeAsync();
            StartBootstrap();
            ForegroundServiceCoordinator.OnAndroidAutoConnected(this);
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
            current = null;
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
                    if (ForegroundServiceCoordinator.CurrentOwner != ForegroundServiceCoordinator.ForegroundServiceOwner.AndroidAuto)
                    {
                        ForegroundServiceOperations.StopForeground(this);
                    }

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

    private sealed class ForegroundStartListener : Java.Lang.Object, MediaSessionService.IListener
    {
        public void OnForegroundServiceStartNotAllowedException()
        {
            logger.Warning("Media3 foreground start was not allowed. Alarm foreground service must stay up until playback is running.");
        }
    }
}
