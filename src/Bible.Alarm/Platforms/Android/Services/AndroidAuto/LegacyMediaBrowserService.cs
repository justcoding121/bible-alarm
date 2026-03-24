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

    // Helper classes
    private readonly MediaSessionInitializer mediaSessionInitializer = new(logger);
    private readonly ClientValidator clientValidator = new(logger);
    private readonly MediaBrowser mediaBrowser = new(logger);
    private readonly PlaybackController playbackController = new(logger);
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
            logger.Warning(ex, "LegacyMediaBrowserService.OnCreate: failed to create MediaSession");
        }

        base.OnCreate();
        logger.Information("LegacyMediaBrowserService.OnCreate() called");

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

            // Initialize state subscription in background
            _ = stateSubscriptionManager.InitializeStateSubscriptionAsync();

            var rotationService = ServiceProviderManager.GetService<IAndroidAutoDefaultScheduleRotationService>();
            if (rotationService != null)
            {
                rotationService.Start();
            }
            else
            {
                logger.Debug("LegacyMediaBrowserService.OnCreate: rotation service not available (DI may not be ready)");
            }

            RefreshDefaultMetadataAfterBootstrap();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "LegacyMediaBrowserService.OnCreate: error during initialization (foreground service is already running)");
        }
    }

    public override BrowserRoot? OnGetRoot(string clientPackageName, int clientUid, Bundle? rootHints)
    {
        var root = clientValidator.ValidateClientAndGetRoot(clientPackageName, clientUid, rootHints);

        // If root is returned (Android Auto is connecting), mark as connected
        if (root != null)
        {
            logger.Information("Android Auto client validated - marking as connected");
            ForegroundServiceCoordinator.OnAndroidAutoConnected(this);
        }

        return root;
    }


    public override void OnLoadChildren(string parentId, Result result)
    {
        logger.Information("✅ OnLoadChildren called for parent: {ParentId}", parentId);

        try
        {
            // CRITICAL: Detach the result before starting async work
            // Android requires that OnLoadChildren either calls SendResult() synchronously
            // or calls Detach() before returning if the result will be sent asynchronously
            result.Detach();
            logger.Debug("Result detached successfully for parent: {ParentId}", parentId);

            // Run work to load schedules and create MediaItems on background thread
            _ = Task.Run(async () => await LoadChildrenAsync(parentId, result));
        }
        catch (Exception ex)
        {
            // If Detach() itself fails, try to send empty result synchronously
            logger.Error(ex, "Critical error in OnLoadChildren before detaching result for parent: {ParentId}", parentId);
            SendEmptyResultSafely(result, parentId);
        }
    }

    private async Task LoadChildrenAsync(string parentId, Result result)
    {
        try
        {
            logger.Debug("Starting schedule loading for parent: {ParentId}", parentId);

            var children = await mediaBrowser.LoadChildrenAsync(parentId, this);
            if (children != null && children.Count > 0)
            {
                var javaList = new JavaList<MediaBrowserCompat.MediaItem>(children);
                logger.Information("Created {Count} MediaItems for Android Auto", javaList.Size());
                result.SendResult(javaList);
            }
            else
            {
                logger.Debug("No MediaItems to send for parent: {ParentId}", parentId);
                result.SendResult(new ArrayList());
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error loading children in LegacyMediaBrowserService for parent: {ParentId}. Bootstrap may not have completed or services may not be available.", parentId);
            SendEmptyResultSafely(result, parentId);
        }
    }


    private static async Task<bool> WaitForBootstrapAsync(string parentId)
    {
        try
        {
            await MauiProgram.WaitForBootstrapAsync();
            logger.Debug("Bootstrap completed, loading schedules from state for parent: {ParentId}", parentId);
            return true;
        }
        catch (Exception bootstrapEx)
        {
            logger.Warning(bootstrapEx, "Bootstrap not ready or timed out for parent: {ParentId} - returning empty list", parentId);
            return false;
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
                if (pState?.Value?.IsPreparingOrPlaying == true)
                {
                    logger.Debug("Playback is active on car connect - skipping default metadata refresh");

                    // MediaElement is handling playback (paused or playing). The minimal
                    // "Starting..." notification (ID 2) from OnCreate is redundant — remove it
                    // so it doesn't persist indefinitely alongside MediaElement's notification.
                    if (ForegroundServiceCoordinator.CurrentOwner != ForegroundServiceCoordinator.ForegroundServiceOwner.AndroidAuto)
                    {
                        ForegroundServiceOperations.StopForeground(this);
                        logger.Information("Stopped stuck minimal foreground notification — MediaElement is handling playback");
                    }
                    return;
                }

                var dispatcher = ServiceProviderManager.GetService<Fluxor.IDispatcher>();
                dispatcher?.Dispatch(new SetCarPlayScreenAction());

                NotifyChildrenChanged("__ID_ROOT__");
                logger.Debug("Default metadata refresh and browse tree update dispatched on car connect");
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Failed to refresh default metadata on car connect");
            }
        });
    }

    private static int GetSectionIconSize()
    {
        return 128;
    }

    private static void SendEmptyResultSafely(Result result, string parentId)
    {
        try
        {
            // Always send a result, even if empty, to satisfy Android's requirement
            // This prevents the IllegalStateException from occurring
            result.SendResult(new ArrayList());
            logger.Debug("Sent empty result for parent: {ParentId}", parentId);
        }
        catch (Exception sendEx)
        {
            logger.Error(sendEx, "Failed to send empty result for parent: {ParentId}. This may cause Android Auto connection issues.", parentId);
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

    private void LogBindIntent(Intent? intent)
    {
        logger.Information("✅ LegacyMediaBrowserService.OnBind() called with intent: {Action}",
            intent?.Action);

        if (intent != null)
        {
            logger.Information("Intent component: {Component}, Package: {Package}, Categories: {Categories}",
                intent.Component?.ClassName, intent.Package, string.Join(", ", intent.Categories ?? Array.Empty<string>()));
        }
    }

    private void EnsureServicePersistence()
    {
        try
        {
            var startIntent = new Intent(this, typeof(LegacyMediaBrowserService));
            StartService(startIntent);
            logger.Debug("Service started to keep it alive during Android Auto connection");
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to start service in OnBind() - service may be destroyed if Android Auto unbinds");
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
                        logger.Information("SessionToken set in OnBind(): {Token}", SessionToken?.ToString() ?? "null");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Could not set SessionToken in OnBind() - bootstrap may still be running");
            }
        }
    }

    public override bool OnUnbind(Intent? intent)
    {
        logger.Information("⚠️ LegacyMediaBrowserService.OnUnbind() called - Client disconnected");

        try
        {
            var rotationService = ServiceProviderManager.GetService<IAndroidAutoDefaultScheduleRotationService>();
            rotationService?.Stop();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "LegacyMediaBrowserService.OnUnbind: failed to stop rotation service");
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
        logger.Information("✅ LegacyMediaBrowserService destroyed - Clearing local MediaSession reference");

        try
        {
            var rotationService = ServiceProviderManager.GetService<IAndroidAutoDefaultScheduleRotationService>();
            rotationService?.Stop();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "LegacyMediaBrowserService.OnDestroy: failed to stop rotation service");
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

