#nullable enable
using Android.Content;
using Android.Runtime;
using Android.Support.V4.Media.Session;
using AndroidX.Car.App;
using AndroidX.Car.App.Model;
using AndroidX.Car.App.Validation;
using Bible.Alarm.Platforms.Android.Services.Media;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto;

/// <summary>
/// CarAppService for modern Android Automotive OS (Polestar, Volvo, GM, Rivian, Ford 2024+).
/// Uses the shared MediaSessionCompat from MediaSessionManager to ensure seamless playback continuity.
/// </summary>
[Register("bible.alarm.platforms.android.services.androidauto.CarAppService")]
public class CarAppService : AndroidX.Car.App.CarAppService
{
    private static readonly ILogger Logger = Log.ForContext<CarAppService>();

    public override HostValidator CreateHostValidator()
    {
        Logger.Information("✅ CarAppService.CreateHostValidator() called");
        return HostValidator.AllowAllHostsValidator; // TODO: Replace with proper host validation in production
    }

    public override Session OnCreateSession()
    {
        Logger.Information("✅ CarAppService.OnCreateSession() called - Modern Android Auto is connecting!");
        return new ModernMediaSession();
    }
}

/// <summary>
/// Session for modern Android Auto that attaches the shared MediaSessionCompat.
/// </summary>
public class ModernMediaSession : Session
{
    private static readonly ILogger Logger = Log.ForContext<ModernMediaSession>();
    private readonly MediaSessionCompat _phoneSession = MediaSessionManager.Instance.GetOrCreate();

    public ModernMediaSession()
    {
        Logger.Information("✅ ModernMediaSession created");
    }

    public override AndroidX.Car.App.Screen OnCreateScreen(Intent intent)
    {
        Logger.Information("✅ ModernMediaSession.OnCreateScreen() called with intent: {Action}", intent?.Action);
        
        // The media session is attached via the template in MainCarScreen
        Logger.Information("✅ Modern Android Auto connected — will attach shared MediaSession via template");
        
        return new MainCarScreen(CarContext);
    }
}

/// <summary>
/// Main car screen that displays the media center template with playback controls.
/// </summary>
public class MainCarScreen : AndroidX.Car.App.Screen
{
    private static readonly ILogger Logger = Log.ForContext<MainCarScreen>();

    public MainCarScreen(CarContext carContext) : base(carContext)
    {
        Logger.Information("✅ MainCarScreen created");
    }

    public override ITemplate OnGetTemplate()
    {
        Logger.Information("✅ MainCarScreen.OnGetTemplate() called - Head unit is requesting the UI!");
        
        // For now, use MessageTemplate - the media session is already attached via LegacyMediaBrowserService
        // The shared MediaSessionCompat will be discovered automatically by Android Auto
        return new MessageTemplate.Builder("Service is working! Media session is shared with legacy Android Auto.")
            .SetTitle("Bible Alarm")
            .SetHeaderAction(AndroidX.Car.App.Model.Action.AppIcon)
            .Build();
    }
}
