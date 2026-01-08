#nullable enable

using Foundation;
using Serilog;
using UIKit;

namespace Bible.Alarm.Platforms.iOS;

/// <summary>
/// Scene Delegate for the main app window.
/// This handles the main app scene lifecycle when using iOS 13+ scene-based architecture.
/// MAUI will still create the window through App.CreateWindow, but this delegate
/// manages the scene lifecycle events.
/// </summary>
[Register("SceneDelegate")]
public class SceneDelegate : UIResponder, IUIWindowSceneDelegate
{
    private static readonly ILogger logger = Log.ForContext<SceneDelegate>();
    private UIWindow? window;

    [Export("scene:willConnectToSession:options:")]
    public void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
    {
        try
        {
            if (scene is UIWindowScene windowScene)
            {
                // MAUI creates the window through App.CreateWindow, so we don't create it here
                // This delegate just manages the scene lifecycle
                logger.Debug("[SceneDelegate] Scene will connect to session: {SessionRole}", session.Role);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "[SceneDelegate] Error in WillConnect");
        }
    }

    [Export("sceneDidDisconnect:")]
    public void DidDisconnect(UIScene scene)
    {
        try
        {
            logger.Debug("[SceneDelegate] Scene did disconnect");
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "[SceneDelegate] Error in DidDisconnect");
        }
    }

    [Export("sceneDidBecomeActive:")]
    public void DidBecomeActive(UIScene scene)
    {
        try
        {
            logger.Debug("[SceneDelegate] Scene did become active");
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "[SceneDelegate] Error in DidBecomeActive");
        }
    }

    [Export("sceneWillResignActive:")]
    public void WillResignActive(UIScene scene)
    {
        try
        {
            logger.Debug("[SceneDelegate] Scene will resign active");
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "[SceneDelegate] Error in WillResignActive");
        }
    }

    [Export("sceneWillEnterForeground:")]
    public void WillEnterForeground(UIScene scene)
    {
        try
        {
            logger.Debug("[SceneDelegate] Scene will enter foreground");
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "[SceneDelegate] Error in WillEnterForeground");
        }
    }

    [Export("sceneDidEnterBackground:")]
    public void DidEnterBackground(UIScene scene)
    {
        try
        {
            logger.Debug("[SceneDelegate] Scene did enter background");
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "[SceneDelegate] Error in DidEnterBackground");
        }
    }

    public UIWindow? Window
    {
        get => window;
        set => window = value;
    }
}
