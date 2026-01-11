#nullable enable

using Foundation;
using Serilog;
using UIKit;

namespace Bible.Alarm.Platforms.iOS;

/// <summary>
/// Scene Delegate for the main app window.
/// Inherits from MauiUISceneDelegate to properly integrate with MAUI's window creation.
/// This is required when using iOS 13+ scene-based architecture with CarPlay support.
/// </summary>
[Register("SceneDelegate")]
public class SceneDelegate : MauiUISceneDelegate
{
    private static readonly ILogger logger = Log.ForContext<SceneDelegate>();

    public override void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
    {
        try
        {
            logger.Debug("[SceneDelegate] Scene will connect to session: {SessionRole}", session.Role);
            // Call base to let MAUI create the window properly
            base.WillConnect(scene, session, connectionOptions);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "[SceneDelegate] Error in WillConnect");
        }
    }

    public override void DidDisconnect(UIScene scene)
    {
        try
        {
            logger.Debug("[SceneDelegate] Scene did disconnect");
            base.DidDisconnect(scene);
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

    public override void WillEnterForeground(UIScene scene)
    {
        try
        {
            logger.Debug("[SceneDelegate] Scene will enter foreground");
            base.WillEnterForeground(scene);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "[SceneDelegate] Error in WillEnterForeground");
        }
    }

    public override void DidEnterBackground(UIScene scene)
    {
        try
        {
            logger.Debug("[SceneDelegate] Scene did enter background");
            base.DidEnterBackground(scene);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "[SceneDelegate] Error in DidEnterBackground");
        }
    }
}
