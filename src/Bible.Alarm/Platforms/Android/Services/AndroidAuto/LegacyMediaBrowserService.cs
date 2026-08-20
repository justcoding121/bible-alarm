#nullable enable
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using AndroidX.Media;
using AndroidX.Media.Session;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Platforms.Android.Services.AndroidAuto.Interfaces;
using Bible.Alarm.Platforms.Android.Services.AndroidAuto.LegacyMediaBrowserHelpers;
using Bible.Alarm.Platforms.Android.Services.Media;
using Bible.Alarm.Platforms.Android.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Playback;
using Fluxor;
using Java.Util;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto;

/// <summary>
/// MediaBrowserService for Android Auto - the sole Android Auto service.
/// Provides media browsing tree, voice commands, recommendations, and playback controls
/// for all Android Auto versions (phone projection + AAOS).
/// Uses the shared MediaSessionCompat from MediaSessionManager.
/// </summary>
[Service(Exported = true, ForegroundServiceType = ForegroundService.TypeMediaPlayback)]
[IntentFilter(["android.media.browse.MediaBrowserService"])]
[Register("bible.alarm.platforms.android.services.androidauto.LegacyMediaBrowserService")]
public class LegacyMediaBrowserService : MediaBrowserServiceCompat
{
    private static readonly ILogger logger = Log.ForContext<LegacyMediaBrowserService>();

    private readonly MediaSessionInitializer mediaSessionInitializer = new(logger);
    private readonly ClientValidator clientValidator = new(logger);
    private readonly MediaBrowser mediaBrowser = new(logger);
    private readonly StateSubscriptionManager stateSubscriptionManager = new(logger);

    // MediaSession references
    private MediaSessionCompat? session;
    private IMediaSessionManager? mediaSessionManager;

    public override void OnCreate()
    {
        // CRITICAL: Start foreground with a minimal notification as the absolute first thing.
        // This prevents the OS from killing the process before any initialization completes.
        // On Android 12+, services must call startForeground() within ~5 seconds of creation.
        // Everything else (MediaSession, DI, bootstrap) happens after this safety net.
        ForegroundServiceOperations.StartForegroundMinimal(this);

        try
        {
            // Create MediaSession (loads last played metadata from Preferences, no DI needed)
            Platforms.Android.Services.Media.MediaSessionHelper.Create();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.AndroidMediaSessionCreationDiagnosticsLog.LegacyMediaBrowserServiceOnCreateFailed);
        }

        base.OnCreate();
        logger.Information(AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.OnCreateCalled);

        try
        {
            // Update the foreground notification with proper MediaStyle and metadata.
            // This replaces the minimal notification with the full Android Auto notification.
            ForegroundServiceCoordinator.OnAndroidAutoConnected(this);

            // Initialize DI and bootstrap (slower, runs after foreground is secured)
            mediaSessionInitializer.InitializeMediaSession();
            mediaSessionInitializer.InitializeBootstrapInBackground();

            // Set the MediaBrowserService instance so StateSubscriptionManager can call NotifyChildrenChanged
            stateSubscriptionManager.SetMediaBrowserService(this);

            _ = stateSubscriptionManager.InitializeStateSubscriptionAsync();

            var rotationService = ServiceProviderManager.GetService<IAndroidAutoDefaultScheduleRotationService>();
            if (rotationService != null)
            {
                rotationService.Start();
            }
            else
            {
                logger.Debug(AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.OnCreateRotationServiceNotAvailable);
            }

            RefreshDefaultMetadataAfterBootstrap();
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.OnCreateErrorDuringInitialization);
        }
    }

    public override BrowserRoot? OnGetRoot(string clientPackageName, int clientUid, Bundle? rootHints)
    {
        var root = clientValidator.ValidateClientAndGetRoot(clientPackageName, clientUid, rootHints);

        // If root is returned (Android Auto is connecting), mark as connected
        if (root != null)
        {
            logger.Information(AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.AndroidAutoClientValidatedMarkingConnected);
            ForegroundServiceCoordinator.OnAndroidAutoConnected(this);
        }

        return root;
    }


    public override void OnLoadChildren(string parentId, Result result)
    {
        logger.Information(AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.OnLoadChildrenCalledForParent, parentId);

        try
        {
            // CRITICAL: Detach the result before starting async work
            // Android requires that OnLoadChildren either calls SendResult() synchronously
            // or calls Detach() before returning if the result will be sent asynchronously
            result.Detach();
            logger.Debug(AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.ResultDetachedSuccessfullyForParent, parentId);

            // Run work to load schedules and create MediaItems on background thread
            _ = Task.Run(async () => await LoadChildrenAsync(parentId, result));
        }
        catch (Exception ex)
        {
            // If Detach() itself fails, try to send empty result synchronously
            logger.Error(ex, AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.CriticalErrorOnLoadChildrenBeforeDetach, parentId);
            SendEmptyResultSafely(result, parentId);
        }
    }

    private async Task LoadChildrenAsync(string parentId, Result result)
    {
        try
        {
            logger.Debug(AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.StartingScheduleLoadingForParent, parentId);

            var children = await mediaBrowser.LoadChildrenAsync(parentId, this);
            if (children != null && children.Count > 0)
            {
                var javaList = new JavaList<MediaBrowserCompat.MediaItem>(children);
                logger.Information(AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.CreatedMediaItemsForAndroidAuto, javaList.Size());
                result.SendResult(javaList);
            }
            else
            {
                logger.Debug(AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.NoMediaItemsToSendForParent, parentId);
                result.SendResult(new ArrayList());
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.ErrorLoadingChildrenForParent, parentId);
            SendEmptyResultSafely(result, parentId);
        }
    }


    private void RefreshDefaultMetadataAfterBootstrap()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await MauiProgram.WaitForBootstrapAsync();

                var pState = ServiceProviderManager.GetService<IState<PlaybackState>>();
                if (pState?.Value?.IsPreparingOrPlaying is true)
                {
                    logger.Debug(AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.PlaybackActiveOnCarConnectSkippingRefresh);

                    // MediaElement is handling playback (paused or playing). The minimal
                    // "Starting..." notification (ID 2) from OnCreate is redundant — remove it
                    // so it doesn't persist indefinitely alongside MediaElement's notification.
                    if (ForegroundServiceCoordinator.CurrentOwner != ForegroundServiceCoordinator.ForegroundServiceOwner.AndroidAuto)
                    {
                        ForegroundServiceOperations.StopForeground(this);
                        logger.Information(AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.StoppedStuckMinimalForegroundNotification);
                    }
                    return;
                }

                var dispatcher = ServiceProviderManager.GetService<Fluxor.IDispatcher>();
                dispatcher?.Dispatch(new SetCarPlayScreenAction());

                NotifyChildrenChanged("__ID_ROOT__");
                logger.Debug(AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.DefaultMetadataRefreshDispatchedOnCarConnect);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.FailedToRefreshDefaultMetadataOnCarConnect);
            }
        });
    }

    private static void SendEmptyResultSafely(Result result, string parentId)
    {
        try
        {
            // Always send a result, even if empty, to satisfy Android's requirement
            // This prevents the IllegalStateException from occurring
            result.SendResult(new ArrayList());
            logger.Debug(AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.SentEmptyResultForParent, parentId);
        }
        catch (Exception sendEx)
        {
            logger.Error(sendEx, AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.FailedToSendEmptyResultForParent, parentId);
        }
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        // Handle media button events (steering wheel buttons, etc.)
        if (session != null)
        {
            MediaButtonReceiver.HandleIntent(session, intent);
        }
        return StartCommandResult.Sticky;
    }

    public override IBinder? OnBind(Intent? intent)
    {
        LogBindIntent(intent);
        EnsureServicePersistence();
        EnsureSessionTokenIsSet();

        // Mark Android Auto as connected when service is bound
        // OnGetRoot may have already called this, but it's safe to call again (idempotent)
        ForegroundServiceCoordinator.OnAndroidAutoConnected(this);

        return base.OnBind(intent);
    }

    private static void LogBindIntent(Intent? intent)
    {
        logger.Information(AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.OnBindCalledWithIntent,
            intent?.Action);

        if (intent != null)
        {
            logger.Information(AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.IntentComponentPackageCategories,
                intent.Component?.ClassName, intent.Package, string.Join(", ", intent.Categories ?? Array.Empty<string>()));
        }
    }

    private void EnsureServicePersistence()
    {
        try
        {
            var startIntent = new Intent(this, typeof(LegacyMediaBrowserService));
            StartService(startIntent);
            logger.Debug(AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.ServiceStartedToKeepAliveDuringAaConnection);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.FailedToStartServiceInOnBind);
        }
    }

    private void EnsureSessionTokenIsSet()
    {
        if (SessionToken == null)
        {
            try
            {
                mediaSessionManager ??= ServiceProviderManager.GetService<IMediaSessionManager>();
                if (mediaSessionManager != null)
                {
                    session ??= mediaSessionManager.GetOrCreate();
                    if (session?.SessionToken != null)
                    {
                        SessionToken = session.SessionToken;
                        logger.Information(AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.SessionTokenSetInOnBind, SessionToken?.ToString() ?? "null");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.CouldNotSetSessionTokenInOnBind);
            }
        }
    }

    public override bool OnUnbind(Intent? intent)
    {
        logger.Information(AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.OnUnbindCalledClientDisconnected);

        try
        {
            var rotationService = ServiceProviderManager.GetService<IAndroidAutoDefaultScheduleRotationService>();
            rotationService?.Stop();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.OnUnbindFailedToStopRotationService);
        }

        // Mark Android Auto as disconnected and stop foreground service
        ForegroundServiceCoordinator.OnAndroidAutoDisconnected();

        // Return base.OnUnbind() to maintain sticky service behavior
        // This allows the service to stay alive for quick reconnections
        // OnDestroy() will serve as a fallback if OnUnbind wasn't called
        // (e.g., when Android Auto emulator is closed abruptly)
        return base.OnUnbind(intent);
    }


    public override void OnDestroy()
    {
        logger.Information(AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.OnDestroyClearingLocalMediaSessionReference);

        try
        {
            var rotationService = ServiceProviderManager.GetService<IAndroidAutoDefaultScheduleRotationService>();
            rotationService?.Stop();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.OnDestroyFailedToStopRotationService);
        }

        // Fallback: If OnUnbind wasn't called (e.g., when Android Auto emulator is closed),
        // mark Android Auto as disconnected and stop foreground service
        // This ensures the notification is removed even if Android doesn't properly call OnUnbind
        ForegroundServiceCoordinator.OnAndroidAutoDisconnected();

        // Clean up state subscriptions
        stateSubscriptionManager.Cleanup();

        // IMPORTANT: Do NOT release the shared MediaSessionCompat here!
        // The MediaSession is managed by MediaSessionManager as a singleton and must persist
        // across service lifecycle changes. Android Auto expects the MediaSession to remain
        // available even when the service is temporarily destroyed and recreated.
        // Only clear the local reference - MediaSessionManager will handle cleanup when appropriate.
        session = null;

        base.OnDestroy();
    }

}

