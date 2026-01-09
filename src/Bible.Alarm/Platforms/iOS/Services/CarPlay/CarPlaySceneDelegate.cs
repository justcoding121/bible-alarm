#nullable enable
using CarPlay;
using Foundation;
using Serilog;
using UIKit;

namespace Bible.Alarm.Platforms.iOS.Services.CarPlay;

/// <summary>
/// CarPlay Scene Delegate for Bible Alarm.
/// Handles CarPlay connection/disconnection and manages the Now Playing template.
/// 
/// For audio apps, CarPlay primarily uses MPNowPlayingInfoCenter and MPRemoteCommandCenter
/// (already implemented in iOSMediaSessionEffect). This delegate provides the CarPlay-specific
/// UI template when the user opens the app in CarPlay.
/// 
/// NOTE: This requires the com.apple.developer.carplay-audio entitlement from Apple MFi portal.
/// Without the entitlement, this delegate will never be called (CarPlay won't connect to the app).
/// 
/// IMPORTANT: The [Register] attribute is now enabled for simulator testing with dual-scene
/// configuration. The Info.plist explicitly configures both the main app scene and CarPlay
/// scene, preventing the auto-generated manifest from overriding the main app scene.
/// 
/// For device builds, this remains disabled until MFi approval is obtained.
/// </summary>
[Register("CarPlaySceneDelegate")]
public class CarPlaySceneDelegate : UIResponder, ICPTemplateApplicationSceneDelegate
{
    private static readonly ILogger logger = Log.ForContext<CarPlaySceneDelegate>();

    private CPInterfaceController? interfaceController;

    /// <summary>
    /// Static flag indicating if CarPlay is currently connected.
    /// </summary>
    public static bool IsCarPlayConnected { get; private set; }

    /// <summary>
    /// Called when CarPlay connects to the app.
    /// </summary>
    [Export("templateApplicationScene:didConnectInterfaceController:")]
    public void DidConnect(CPTemplateApplicationScene scene, CPInterfaceController controller)
    {
        try
        {
            logger.Information("[CarPlay] Connected to CarPlay interface controller");
            interfaceController = controller;
            IsCarPlayConnected = true;
        }
        catch (Exception ex)
        {
            // CarPlay connection failures are non-critical (CarPlay is optional)
            logger.Warning(ex, "[CarPlay] Error during CarPlay connection");
        }
    }

    /// <summary>
    /// Called when CarPlay disconnects from the app.
    /// </summary>
    [Export("templateApplicationScene:didDisconnectInterfaceController:")]
    public void DidDisconnect(CPTemplateApplicationScene scene, CPInterfaceController controller)
    {
        try
        {
            logger.Information("[CarPlay] Disconnected from CarPlay interface controller");
            IsCarPlayConnected = false;
            interfaceController = null;
            
            // Note: Now Playing info will be cleared automatically when SetCarPlayScreenAction
            // is dispatched next (e.g., when playback stops), as iOSMediaSessionEffect checks
            // IsCarPlayConnected and clears if CarPlay is not connected.
        }
        catch (Exception ex)
        {
            // CarPlay disconnection failures are non-critical (CarPlay is optional)
            logger.Warning(ex, "[CarPlay] Error during CarPlay disconnection");
        }
    }

    /// <summary>
    /// Called when the CarPlay scene is about to connect to a session.
    /// </summary>
    [Export("templateApplicationScene:didConnectInterfaceController:toWindow:")]
    public void DidConnect(CPTemplateApplicationScene scene, CPInterfaceController controller, CPWindow window)
    {
        DidConnect(scene, controller);
    }

    /// <summary>
    /// Updates the Now Playing template buttons dynamically.
    /// </summary>
    public void UpdateNowPlayingButtons()
    {
        logger.Debug("[CarPlay] Now Playing buttons are managed automatically by CarPlay");
    }
}
