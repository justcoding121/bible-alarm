#nullable enable
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.Media;
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
        
        try
        {
            // Initialize bootstrap and ensure services are available
            MauiAppHolder.CreateAndStore();
            MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);
            
            // Get MediaSessionManager from service provider
            _mediaSessionManager = ServiceProviderManager.GetService<MediaSessionManager>();
            if (_mediaSessionManager == null)
            {
                Logger.Error("MediaSessionManager is null - cannot initialize MediaSessionCompat");
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
            
            Logger.Information("✅ LegacyMediaBrowserService.OnCreate() called - Legacy Android Auto is connecting! SessionToken set correctly. Bootstrap initialized.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error initializing LegacyMediaBrowserService");
            throw;
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
        
        // TODO: Build your real hierarchy here with playlists, tracks, etc.
        // For now, return empty list - will be populated with actual schedules later
        var list = new Java.Util.ArrayList();
        result.SendResult(list);
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

