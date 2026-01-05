#nullable enable
using _Microsoft.Android.Resource.Designer;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using AndroidX.Core.Content;
using AndroidX.Media;
using AndroidX.Media.Session;
using Bible.Alarm.Common;
using Bible.Alarm.Platforms.Android.Services.AndroidAuto.LegacyMediaBrowserHelpers;
using Bible.Alarm.Platforms.Android.Services.Media;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Java.Util;
using Serilog;
using Color = Android.Graphics.Color;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto;

/// <summary>
/// MediaBrowserService for Android Auto - Mandatory backend for all Android Auto versions.
/// 
/// Purpose:
/// - Primary interface for older Android Auto versions (phone projection, old DHU, 2016–2024 cars)
/// - Backend for voice commands, recommendations, and playback controls in newer versions
/// - Works alongside CarAppService: CarAppService handles UI, this handles playback/voice
/// 
/// Strategy: Dual Support
/// - CarAppService: Handles templated UI for browsing and playback screens (CAL API 8+)
/// - MediaBrowserService: Mandatory backend for voice commands, recommendations, and playback controls
/// 
/// Uses the shared MediaSessionCompat from MediaSessionManager to ensure seamless playback continuity
/// across both services.
/// </summary>
[Service(Exported = true)]
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
    private Bible.Alarm.Platforms.Android.Services.Media.MediaSessionManager? mediaSessionManager;

    public override void OnCreate()
    {
        // Create MediaSession as the very first thing - even before MAUI services are registered
        // This ensures MediaSession is available immediately on process start
        try
        {
            Platforms.Android.Services.Media.MediaSessionHelper.Create();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "LegacyMediaBrowserService.OnCreate: failed to create MediaSession");
        }

        base.OnCreate();
        logger.Information("LegacyMediaBrowserService.OnCreate() called - Ensuring MauiApp is created");

        mediaSessionInitializer.InitializeMediaSession();
        mediaSessionInitializer.InitializeBootstrapInBackground();

        // Set the MediaBrowserService instance so StateSubscriptionManager can call NotifyChildrenChanged
        stateSubscriptionManager.SetMediaBrowserService(this);

        // Initialize state subscription in background
        _ = stateSubscriptionManager.InitializeStateSubscriptionAsync();
    }

    public override BrowserRoot? OnGetRoot(string clientPackageName, int clientUid, Bundle? rootHints)
    {
        return clientValidator.ValidateClientAndGetRoot(clientPackageName, clientUid, rootHints);
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
            await MauiProgram.WaitForBootstrapAsync(timeoutMs: 30000); // Use standard 30 second timeout
            logger.Debug("Bootstrap completed, loading schedules from state for parent: {ParentId}", parentId);
            return true;
        }
        catch (Exception bootstrapEx)
        {
            logger.Warning(bootstrapEx, "Bootstrap not ready or timed out for parent: {ParentId} - returning empty list", parentId);
            return false;
        }
    }





    private static int GetBookIconSize()
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
                mediaSessionManager ??= ServiceProviderManager.GetService<Bible.Alarm.Platforms.Android.Services.Media.MediaSessionManager>();
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

        // Note: Even though a client unbinds, the service may not be destroyed immediately
        // because we start it as a sticky service in OnBind(). This prevents premature destruction
        // when Android Auto temporarily disconnects and reconnects.
        // The service will only be destroyed if StopService() is explicitly called or the system
        // needs to reclaim resources (which is rare for sticky services).

        return base.OnUnbind(intent);
    }


    public override void OnDestroy()
    {
        logger.Information("✅ LegacyMediaBrowserService destroyed - Clearing local MediaSession reference");

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

