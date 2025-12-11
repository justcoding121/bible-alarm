#nullable enable
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.Media.Session;
using AndroidX.Media;
using AndroidX.Media.Session;
using Bible.Alarm.Common;
using Serilog;

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
[IntentFilter(new[] { "android.media.browse.MediaBrowserService" })]
[Register("bible.alarm.platforms.android.services.androidauto.LegacyMediaBrowserService")]
public class LegacyMediaBrowserService : MediaBrowserServiceCompat
{
    private static readonly ILogger Logger = Log.ForContext<LegacyMediaBrowserService>();
    private MediaSessionCompat? _session;
    private MediaSessionManager? _mediaSessionManager;

    public override void OnCreate()
    {
        base.OnCreate();
        
        Logger.Information("LegacyMediaBrowserService.OnCreate() called - Initializing bootstrap (background service)");
        
        // Initialize bootstrap asynchronously to avoid blocking UI thread
        // Android Auto services can be created during app startup, so we must not block
        _ = Task.Run(async () =>
        {
            try
            {
                MauiAppHolder.CreateAndStore();
                MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);
                
                // Wait for bootstrap to complete before proceeding
                // This ensures database migrations are finished before accessing schedule database
                await MauiProgram.WaitForBootstrapAsync();
                
                Logger.Information("✅ LegacyMediaBrowserService bootstrap initialized and ready");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error initializing bootstrap in LegacyMediaBrowserService");
            }
        });
        
        // Initialize MediaSession synchronously (required for SessionToken)
        // This will work even if bootstrap is still running
        try
        {
            // Get MediaSessionManager from service provider (may be null if bootstrap not complete)
            _mediaSessionManager = ServiceProviderManager.GetService<MediaSessionManager>();
            if (_mediaSessionManager == null)
            {
                Logger.Warning("MediaSessionManager is null - bootstrap may not be complete yet. Will retry when service is bound.");
                return;
            }
            
            _session = _mediaSessionManager.GetOrCreate(true);
            if (_session == null)
            {
                Logger.Error("MediaSessionCompat is null after GetOrCreate() - cannot set SessionToken");
                return;
            }
            
            // Verify SessionToken is available before setting it
            if (_session.SessionToken == null)
            {
                Logger.Error("MediaSessionCompat.SessionToken is null - MediaSessionCompat may not be properly initialized");
                return;
            }
            
            // THIS IS THE KEY LINE — both systems now see the same session
            SessionToken = _session.SessionToken;
            
            Logger.Information("SessionToken successfully set: {Token}", SessionToken?.ToString() ?? "null");
            Logger.Information("✅ LegacyMediaBrowserService.OnCreate() completed - Legacy Android Auto is connecting! SessionToken set correctly.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error initializing MediaSession in LegacyMediaBrowserService - will retry when service is bound");
        }
    }

    public override MediaBrowserServiceCompat.BrowserRoot? OnGetRoot(string clientPackageName, int clientUid, Bundle? rootHints)
    {
        Logger.Information("✅ OnGetRoot called for client: {ClientPackageName} (UID: {ClientUid})", 
            clientPackageName, clientUid);
        
        // Standard media root ID for Android Auto/AAOS compatibility
        // The root ID "__ID_ROOT__" is a common practice for media apps
        // Android Auto and AAOS hosts are trusted by default when connecting to MediaBrowserService
        return new MediaBrowserServiceCompat.BrowserRoot("__ID_ROOT__", null);
    }

    public override void OnLoadChildren(string parentId, MediaBrowserServiceCompat.Result result)
    {
        Logger.Information("✅ OnLoadChildren called for parent: {ParentId}", parentId);
        
        // Ensure bootstrap is complete before accessing database services
        // This is critical for loading schedules to show in Android Auto
        Task.Run(async () =>
        {
            try
            {
                // Wait for bootstrap to complete before accessing schedule database
                await MauiProgram.WaitForBootstrapAsync();
                
                // TODO: Build your real hierarchy here with schedules
                // Load schedules from database and create MediaItem objects for each schedule
                // Each MediaItem should have the scheduleId as the mediaId so tapping it plays that schedule
                // For now, return empty list - will be populated with actual schedules later
                var list = new Java.Util.ArrayList();
                result.SendResult(list);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading children in LegacyMediaBrowserService");
                result.SendResult(new Java.Util.ArrayList());
            }
        });
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        // Handle media button events (steering wheel buttons, etc.)
        if (_session != null)
        {
            MediaButtonReceiver.HandleIntent(_session, intent);
        }
        return StartCommandResult.Sticky;
    }

    public override IBinder? OnBind(Intent? intent)
    {
        Logger.Information("✅ LegacyMediaBrowserService.OnBind() called with intent: {Action}", 
            intent?.Action);
        
        // Log the intent details to help debug binder conflicts
        if (intent != null)
        {
            Logger.Information("Intent component: {Component}, Package: {Package}, Categories: {Categories}", 
                intent.Component?.ClassName, intent.Package, string.Join(", ", intent.Categories ?? Array.Empty<string>()));
        }
        
        // If SessionToken wasn't set in OnCreate() (bootstrap may not have completed), try to set it now
        if (SessionToken == null)
        {
            try
            {
                _mediaSessionManager ??= ServiceProviderManager.GetService<MediaSessionManager>();
                if (_mediaSessionManager != null)
                {
                    _session ??= _mediaSessionManager.GetOrCreate(true);
                    if (_session?.SessionToken != null)
                    {
                        SessionToken = _session.SessionToken;
                        Logger.Information("SessionToken set in OnBind(): {Token}", SessionToken?.ToString() ?? "null");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Could not set SessionToken in OnBind() - bootstrap may still be running");
            }
        }
        
        return base.OnBind(intent);
    }

    public override void OnDestroy()
    {
        Logger.Information("✅ LegacyMediaBrowserService destroyed - Releasing MediaSession");
        
        // Release the shared MediaSessionCompat to clean up resources
        // This ensures proper cleanup when the service is stopped by the system
        _session?.Release();
        _session = null;
        
        base.OnDestroy();
    }
}

