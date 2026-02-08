#nullable enable

using Foundation;
using Bible.Alarm.Platforms.iOS.Helpers;
using Serilog;
using UIKit;

namespace Bible.Alarm.Platforms.iOS;

/// <summary>
/// Scene Delegate for the main app window.
/// Inherits from MauiUISceneDelegate so MAUI creates the window when
/// the scene configuration name is __MAUI_DEFAULT_SCENE_CONFIGURATION__.
/// Required for scene-based lifecycle (needed to support CarPlay as a second scene).
/// </summary>
[Register("SceneDelegate")]
[Preserve(AllMembers = true)]
public class SceneDelegate : MauiUISceneDelegate
{
    private static readonly ILogger logger = Log.ForContext<SceneDelegate>();

    public override void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
    {
        try
        {
            // Let MAUI create the window. MauiUISceneDelegate.WillConnect checks that
            // session.Configuration.Name == "__MAUI_DEFAULT_SCENE_CONFIGURATION__" and
            // then calls CreatePlatformWindow, which invokes Application.CreateWindow.
            base.WillConnect(scene, session, connectionOptions);
            logger.Information("[SceneDelegate] Scene connected, config: {ConfigName}", session.Configuration.Name);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[SceneDelegate] Error in WillConnect");
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
            // Ensure the scheduler refresh BG task is (re)scheduled while we still have foreground execution time.
            iOSBackgroundTaskScheduler.ScheduleSchedulerRefresh();
            base.DidEnterBackground(scene);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "[SceneDelegate] Error in DidEnterBackground");
        }
    }
}
