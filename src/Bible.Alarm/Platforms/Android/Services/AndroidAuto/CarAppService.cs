#nullable enable
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.Media.Session;
using AndroidX.Car.App;
using AndroidX.Car.App.Model;
using AndroidX.Car.App.Validation;
using Bible.Alarm.Platforms.Android.Services.AndroidAuto;
using Bible.Alarm.Common;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto;

/// <summary>
/// CarAppService for modern Android Automotive OS (Polestar, Volvo, GM, Rivian, Ford 2024+).
/// Uses the shared MediaSessionCompat from MediaSessionManager to ensure seamless playback continuity.
/// 
/// Strategy: Dual Support
/// - CarAppService: Handles templated UI for browsing and playback screens (CAL API 8+)
/// - MediaBrowserService: Mandatory backend for voice commands, recommendations, and playback controls
/// 
/// The host (Android Auto/AAOS) determines which service to bind to based on capability:
/// - Newer systems: Use CarAppService for UI, MediaBrowserService for playback
/// - Older systems: Fall back to MediaBrowserService for everything
/// </summary>
[Service(Exported = true, Name = "bible.alarm.platforms.android.services.androidauto.CarAppService")]
// CRITICAL: MEDIA category removed to prevent phone projection Android Auto from discovering this service
// Phone projection Android Auto should only discover LegacyMediaBrowserService
// Modern AAOS will discover this via androidx.car.app.host.description metadata
[IntentFilter(new[] { "androidx.car.app.CarAppService" })]
// Links the CarAppService to the description file for modern Android Auto/AAOS discovery
[MetaData("androidx.car.app.host.description", Resource = "@xml/car_app_desc")]
// Declare minimum Car App Library API level (using integer resource)
[MetaData("androidx.car.app.minCarApiLevel", Resource = "@integer/car_app_min_api_level")]
// Declare target Car App Library API level (using integer resource)
[MetaData("androidx.car.app.targetCarApiLevel", Resource = "@integer/car_app_target_api_level")]
[Register("bible.alarm.platforms.android.services.androidauto.CarAppService")]
public class CarAppService : AndroidX.Car.App.CarAppService
{
    private static readonly ILogger Logger = Log.ForContext<CarAppService>();

    public override void OnCreate()
    {
        base.OnCreate();
        
        // Initialize bootstrap and ensure services are available
        MauiAppHolder.CreateAndStore();
        MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);
        
        Logger.Information("✅ CarAppService.OnCreate() called - Bootstrap initialized");
    }

    public override HostValidator CreateHostValidator()
    {
        Logger.Information("✅ CarAppService.CreateHostValidator() called");
        return HostValidator.AllowAllHostsValidator; // TODO: Replace with proper host validation in production
    }

    public override Session OnCreateSession()
    {
        try
        {
            Logger.Information("✅ CarAppService.OnCreateSession() called - Modern Android Auto is connecting!");
            // Get ModernMediaSession from service provider
            var session = ServiceProviderManager.GetService<ModernMediaSession>();
            if (session == null)
            {
                Logger.Error("ModernMediaSession is null - cannot create session");
                throw new InvalidOperationException("ModernMediaSession is null");
            }
            Logger.Information("✅ CarAppService.OnCreateSession() completed successfully");
            return session;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "❌ CarAppService.OnCreateSession() failed - This may indicate a Binder interface mismatch");
            throw;
        }
    }
}

/// <summary>
/// Session for modern Android Auto that attaches the shared MediaSessionCompat.
/// </summary>
public class ModernMediaSession : Session
{
    private static readonly ILogger Logger = Log.ForContext<ModernMediaSession>();
    private readonly MediaSessionCompat _phoneSession;
    private readonly MediaSessionManager _mediaSessionManager;

    public ModernMediaSession(MediaSessionManager mediaSessionManager)
    {
        _mediaSessionManager = mediaSessionManager ?? throw new ArgumentNullException(nameof(mediaSessionManager));
        _phoneSession = mediaSessionManager.GetOrCreate(true);
        Logger.Information("✅ ModernMediaSession created");
    }

    public override AndroidX.Car.App.Screen OnCreateScreen(Intent intent)
    {
        Logger.Information("✅ ModernMediaSession.OnCreateScreen() called with intent: {Action}", intent?.Action);
        
        // The media session is attached via the template in MainCarScreen
        Logger.Information("✅ Modern Android Auto connected — will attach shared MediaSession via template");
        
        return new MainCarScreen(CarContext, _mediaSessionManager);
    }
}

/// <summary>
/// Main car screen that displays the media center template with playback controls.
/// </summary>
public class MainCarScreen : AndroidX.Car.App.Screen
{
    private static readonly ILogger Logger = Log.ForContext<MainCarScreen>();
    private readonly MediaSessionManager _mediaSessionManager;

    public MainCarScreen(CarContext carContext, MediaSessionManager mediaSessionManager) : base(carContext)
    {
        _mediaSessionManager = mediaSessionManager ?? throw new ArgumentNullException(nameof(mediaSessionManager));
        Logger.Information("✅ MainCarScreen created");
    }

    public override ITemplate OnGetTemplate()
    {
        Logger.Information("✅ MainCarScreen.OnGetTemplate() called - Head unit is requesting the UI!");
        
        try
        {
            // Get the MediaSessionCompat to verify it's initialized
            var mediaSession = _mediaSessionManager.GetOrCreate();
            var sessionToken = mediaSession.SessionToken;
            
            if (sessionToken == null)
            {
                Logger.Warning("MediaSessionCompat.SessionToken is null - MediaSession may not be fully initialized yet");
            }
            else
            {
                Logger.Information("✅ MediaSession token available: {Token}", sessionToken.ToString());
            }
            
            // For Android Auto Car App Library, we use PaneTemplate to display media information
            // The MediaSession is automatically discovered by Android Auto through the MediaBrowserService
            // This template provides a simple UI while playback controls come from the MediaSession
            
            Logger.Information("✅ Creating PaneTemplate for Android Auto");
            
            // Create a Pane with content - required for PaneTemplate.Builder
            var pane = new Pane.Builder()
                .SetLoading(false)
                .Build();
            
            // Create a PaneTemplate with media information
            // Android Auto will automatically show playback controls from the MediaSession via MediaBrowserService
            // The MediaSession is shared between both services, so Android Auto can control playback
            var paneTemplate = new PaneTemplate.Builder(pane)
                .SetTitle("Bible Alarm")
                .SetHeaderAction(AndroidX.Car.App.Model.Action.AppIcon)
                .Build();
            
            Logger.Information("✅ PaneTemplate created successfully - Android Auto should now display the app");
            return paneTemplate;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "❌ Error creating template - falling back to MessageTemplate");
            // Fallback to message template on error
            return new MessageTemplate.Builder("Bible Alarm")
                .SetTitle("Bible Alarm")
                .SetHeaderAction(AndroidX.Car.App.Model.Action.AppIcon)
                .Build();
        }
    }
}
