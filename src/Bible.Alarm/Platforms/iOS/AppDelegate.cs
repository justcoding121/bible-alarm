#nullable enable

using BackgroundTasks;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Platforms.iOS.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using CommunityToolkit.Mvvm.Messaging;
using Foundation;
using Serilog;
using UIKit;
using UserNotifications;

namespace Bible.Alarm.Platforms.iOS;

/// <summary>
/// The UIApplicationDelegate for the application. This class is responsible for launching the 
/// User Interface of the application, as well as listening (and optionally responding) to 
/// application events from iOS.
/// </summary>
[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate, IUNUserNotificationCenterDelegate
{
    private static readonly ILogger logger = Log.ForContext<AppDelegate>();

    public AppDelegate()
    {
        LogSetup.Initialize(new AssemblyAppVersionFinder(), [], AppConstants.Platform.IOs);
        AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        ObjCRuntime.Runtime.MarshalManagedException += OnMarshalManagedException;
        ObjCRuntime.Runtime.MarshalObjectiveCException += OnMarshalObjectiveCException;
    }

    private const int CrashFlushDelayMs = 500;

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        IOsBootstrapLogger.WriteException(e.Exception);
        logger.Error(e.Exception, AppConstants.Logging.ProcessDiagnosticsLog.UnobservedTaskException);
        FlushAndDelay();
    }

    private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        if (exception != null)
        {
            IOsBootstrapLogger.WriteException(exception);
            logger.Error(exception, AppConstants.Logging.ProcessDiagnosticsLog.UnhandledExceptionIsTerminating, e.IsTerminating);
        }
        else
        {
            IOsBootstrapLogger.WriteLine($"Unhandled non-Exception: {e.ExceptionObject}. IsTerminating: {e.IsTerminating}");
            logger.Error(AppConstants.Logging.ProcessDiagnosticsLog.UnhandledNonExceptionObjectIsTerminating,
                e.ExceptionObject, e.IsTerminating);
        }

        FlushAndDelay();
    }

    private static void OnMarshalManagedException(object? sender, ObjCRuntime.MarshalManagedExceptionEventArgs args)
    {
        logger.Error(args.Exception, AppConstants.Logging.ProcessDiagnosticsLog.ManagedExceptionMarshalingToObjCMode, args.ExceptionMode);
        FlushAndDelay();
    }

    private static void OnMarshalObjectiveCException(object? sender, ObjCRuntime.MarshalObjectiveCExceptionEventArgs args)
    {
        logger.Error(AppConstants.Logging.ProcessDiagnosticsLog.ObjCExceptionCaughtModeException, args.ExceptionMode, args.Exception?.ToString() ?? "null");
        FlushAndDelay();
    }

    private static void FlushAndDelay()
    {
        try
        {
            Log.CloseAndFlush();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.IosAppDelegateDiagnosticsLog.LogCloseAndFlushFailedCrashPath);
        }

        Thread.Sleep(CrashFlushDelayMs);
    }

    protected override MauiApp CreateMauiApp()
    {
        try
        {
            return MauiAppHolder.CreateAndStore();
        }
        catch (Exception e)
        {
            throw new InvalidOperationException(
                AppConstants.Logging.IosAppDelegateDiagnosticsLog.IosMauiAppCreationFailed,
                e);
        }
    }

    /// <summary>
    /// Returns the scene configuration for connecting scene sessions.
    /// For the main app window, delegates to MAUI's base implementation which returns
    /// the __MAUI_DEFAULT_SCENE_CONFIGURATION__ config that MauiUISceneDelegate expects.
    /// For CarPlay, returns a custom config pointing to CarPlaySceneDelegate.
    /// </summary>
    public override UISceneConfiguration GetConfiguration(UIApplication application, UISceneSession connectingSceneSession, UISceneConnectionOptions options)
    {
        // CarPlay scene: return a config with our CarPlaySceneDelegate
        if (connectingSceneSession.Role.ToString().Contains("CPTemplate"))
        {
            logger.Information(AppConstants.Logging.IosAppDelegateDiagnosticsLog.ReturningCarPlaySceneConfiguration);
            var carPlayConfig = new UISceneConfiguration("CarPlayConfiguration", connectingSceneSession.Role);
            carPlayConfig.DelegateClass = new ObjCRuntime.Class(typeof(Services.CarPlay.CarPlaySceneDelegate));
            return carPlayConfig;
        }

        // Main app window: let MAUI handle it (returns __MAUI_DEFAULT_SCENE_CONFIGURATION__)
        return base.GetConfiguration(application, connectingSceneSession, options);
    }

    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        // With UIApplicationSceneManifest in Info.plist, MAUI's base.FinishedLaunching
        // will NOT create a window here. Window creation is handled by
        // SceneDelegate.WillConnect (via MauiUISceneDelegate) when iOS creates the scene.
        bool result;
        try
        {
            result = base.FinishedLaunching(application, launchOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                AppConstants.Logging.IosAppDelegateDiagnosticsLog.BaseFinishedLaunchingThrewException,
                ex);
        }

        try
        {
            MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: true);
            SetupBackgroundTasks();
            SetupNotifications();
        }
        catch (Exception e)
        {
            logger.Error(e, AppConstants.Logging.IosAppDelegateDiagnosticsLog.IosApplicationCustomInitializationFailed);
        }

        return result;
    }

    private static void SetupBackgroundTasks()
    {
        try
        {
            // iOS 16+ (minimum supported) uses BGTaskScheduler for periodic work.
            // This runs the scheduler, which also performs media cache cleanup.
            iOSBackgroundTaskScheduler.RegisterAndSchedule();
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.IosAppDelegateDiagnosticsLog.FailedToSetupIosBackgroundTasks);
        }
    }

    private void SetupNotifications()
    {
        // Set delegate for handling notifications, but do NOT request permission on app start
        // Permission should only be requested when user explicitly enables reminder or taps notification permission button
        UNUserNotificationCenter.Current.Delegate = this;
    }

    public override void OnActivated(UIApplication application)
    {
        try
        {
            HandleDeliveredNotifications();
            ResetBadgeCountStatic();
        }
        catch (Exception e)
        {
            logger.Error(e, AppConstants.Logging.IosAppDelegateDiagnosticsLog.ErrorWhenShowingNotificationOnIosActivation);
        }

        base.OnActivated(application);
    }

    private static void HandleDeliveredNotifications()
    {
        var delivered = UNUserNotificationCenter.Current.GetDeliveredNotificationsAsync().Result;

        if (delivered is { Length: > 0 })
        {
            var notification = delivered[0];
            HandleNotification(notification.Request.Content.UserInfo);

            UNUserNotificationCenter.Current.RemoveAllDeliveredNotifications();
        }
    }

    private static void ResetBadgeCountStatic()
    {
        UNUserNotificationCenter.Current.SetBadgeCount(0, error =>
        {
            if (error != null)
            {
                logger.Error(AppConstants.Logging.IosAppDelegateDiagnosticsLog.FailedToResetBadgeCount, error.LocalizedDescription);
            }
        });
    }

    /// <summary>
    /// Called when the user taps a notification (including when app is launched from notification).
    /// </summary>
    public void DidReceiveNotificationResponse(UNUserNotificationCenter center, UNNotificationResponse response, Action completionHandler)
    {
        try
        {
            var userInfo = response.Notification.Request.Content.UserInfo;
            HandleNotification(userInfo);
        }
        catch (Exception e)
        {
            logger.Error(e, AppConstants.Logging.IosAppDelegateDiagnosticsLog.ErrorHandlingIosNotificationResponse);
        }
        finally
        {
            completionHandler?.Invoke();
        }
    }

    /// <summary>
    /// Called when a notification is delivered while the app is in the foreground.
    /// </summary>
    public void WillPresentNotification(UNUserNotificationCenter center, UNNotification notification, Action<UNNotificationPresentationOptions> completionHandler)
    {
        var options = UNNotificationPresentationOptions.Banner | UNNotificationPresentationOptions.Sound | UNNotificationPresentationOptions.Badge;
        completionHandler(options);
    }

    private static void HandleNotification(NSDictionary userInfo)
    {
        try
        {
            // Reset badge count
            UNUserNotificationCenter.Current.SetBadgeCount(0, error =>
            {
                if (error != null)
                {
                    logger.Error(AppConstants.Logging.IosAppDelegateDiagnosticsLog.FailedToResetBadgeCount, error.LocalizedDescription);
                }
            });

            // Extract schedule ID from notification and start playback
            if (userInfo != null && userInfo.TryGetValue(new NSString("ScheduleId"), out var scheduleIdValue))
            {
                var scheduleIdString = scheduleIdValue?.ToString();
                if (!string.IsNullOrEmpty(scheduleIdString) && int.TryParse(scheduleIdString, out var scheduleId) && scheduleId > 0)
                {
                    logger.Information(AppConstants.Logging.IosAppDelegateDiagnosticsLog.NotificationTappedStartingPlayback, scheduleId);
                    StartPlaybackFromNotification(scheduleId);
                }
            }
        }
        catch (Exception e)
        {
            logger.Error(e, AppConstants.Logging.IosAppDelegateDiagnosticsLog.ErrorHandlingIosNotification);
        }
    }

    /// <summary>
    /// Starts playback when user taps on a notification.
    /// Unlike Android, iOS doesn't support auto-playback from background - user must tap notification.
    /// </summary>
    private static void StartPlaybackFromNotification(int scheduleId)
    {
        Task.Run(async () =>
        {
            try
            {
                // Ensure bootstrap is complete before accessing services
                await MauiProgram.WaitForBootstrapAsync();

                var playbackService = ServiceProviderManager.GetService<ISchedulePlaybackService>();
                if (playbackService != null)
                {
                    WeakReferenceMessenger.Default.Send(new RequestShowPlaybackModalMessage { TargetScheduleId = scheduleId });
                    await playbackService.PlayScheduleAsync(scheduleId);
                    logger.Information(AppConstants.Logging.IosAppDelegateDiagnosticsLog.StartedPlaybackFromNotificationTap, scheduleId);
                }
                else
                {
                    logger.Warning(AppConstants.Logging.IosAppDelegateDiagnosticsLog.ISchedulePlaybackServiceNotAvailableNotificationPlayback);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, AppConstants.Logging.IosAppDelegateDiagnosticsLog.ErrorStartingPlaybackFromNotificationForSchedule, scheduleId);
            }
        });
    }

    public override void PerformFetch(UIApplication application,
        Action<UIBackgroundFetchResult> completionHandler)
    {
        _ = Task.Run(async () =>
        {
            var downloaded = await PerformBackgroundFetchAsyncStatic();
            completionHandler(downloaded ? UIBackgroundFetchResult.NewData : UIBackgroundFetchResult.NoData);
        });
    }

    private static async Task<bool> PerformBackgroundFetchAsyncStatic()
    {
        var downloaded = false;

        try
        {
            MauiAppHolder.CreateAndStore();
            MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);
            await MauiProgram.WaitForBootstrapAsync();

            var schedulerService = ServiceProviderManager.GetService<ISchedulerService>();
            downloaded = await schedulerService.HandleAsync();
        }
        catch (Exception e)
        {
            logger.Error(e, AppConstants.Logging.IosAppDelegateDiagnosticsLog.ErrorPerformFetchTask);
        }

        return downloaded;
    }


    private bool disposed;

    protected override void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException -= UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        ObjCRuntime.Runtime.MarshalManagedException -= OnMarshalManagedException;
        ObjCRuntime.Runtime.MarshalObjectiveCException -= OnMarshalObjectiveCException;

        disposed = true;

        base.Dispose(disposing);
    }
}
