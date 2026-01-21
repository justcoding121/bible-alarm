#nullable enable

using BackgroundTasks;
using Bible.Alarm.Common;
using Bible.Alarm.Platforms.iOS.Services.Platform;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
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
        LogSetup.Initialize(IOsVersionFinder.Default, [], "iOS");
        AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException += UnobserverdTaskException;
    }

    private void UnobserverdTaskException(object? sender, UnobservedTaskExceptionEventArgs e) =>
        logger.Error(e.Exception, "Unobserved task exception.");

    private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
    {
        logger.Error(e.ExceptionObject as Exception, "Unhandled exception. IsTerminating: {IsTerminating}",
            e.IsTerminating);
    }

    protected override MauiApp CreateMauiApp()
    {
        try
        {
            return MauiAppHolder.CreateAndStore();
        }
        catch (Exception e)
        {
            logger.Fatal(e, "iOS MAUI app creation failed.");
            throw;
        }
    }

    public override bool FinishedLaunching(UIApplication app, NSDictionary? launchOptions)
    {
        // Call base.FinishedLaunching FIRST - this creates the Application instance and window
        bool result;
        try
        {
            result = base.FinishedLaunching(app, launchOptions);
        }
        catch (Exception ex)
        {
            logger.Fatal(ex, "base.FinishedLaunching threw exception.");
            throw;
        }

        // Now that the window is created, do custom initialization
        try
        {
            // Initialize bootstrap for foreground launch (after window is created)
            MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: true);

            SetupBackgroundTasks();
            SetupNotifications();
        }
        catch (Exception e)
        {
            logger.Error(e, "iOS application custom initialization failed.");
            // Don't throw - window is already created, just log the error
        }

        return result;
    }

    private void SetupBackgroundTasks()
    {
        // Note: Background fetch is now handled by BGAppRefreshTask in iOS 13+
        if (!UIDevice.CurrentDevice.CheckSystemVersion(13, 0))
        {
            UIApplication.SharedApplication.SetMinimumBackgroundFetchInterval(60 * 60);
        }
    }

    private void SetupNotifications()
    {
        UNUserNotificationCenter.Current.Delegate = this;
        UNUserNotificationCenter.Current.RequestAuthorization(
            UNAuthorizationOptions.Alert
            | UNAuthorizationOptions.Sound
            | UNAuthorizationOptions.Badge, HandleNotificationAuthorizationResponse);
    }

    private void HandleNotificationAuthorizationResponse(bool approved, NSError? error)
    {
        if (!approved)
        {
            Task.Run(async () =>
            {
                try
                {
                    await ShowNotificationDisabledMessageAsync();
                }
                catch (Exception e)
                {
                    logger.Error(e, "Error when prompting iOS notification permission on launch.");
                }
            });
        }
    }

    private async Task ShowNotificationDisabledMessageAsync()
    {
        MauiAppHolder.CreateAndStore();
        MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);
        await MauiProgram.WaitForBootstrapAsync();

        var generalSettingsService = ServiceProviderManager.GetService<IGeneralSettingsService>();
        if (generalSettingsService != null)
        {
            if (!await generalSettingsService.GeneralSettingExistsAsync("iOSNotificationDisabledMsgShown"))
            {
                await generalSettingsService.SetGeneralSettingAsync(
                    "iOSNotificationDisabledMsgShown",
                    "true");

                var popupService = ServiceProviderManager.GetService<IToastService>();
                await popupService.ShowMessage("You've disabled notifications. " +
                                         "We won't be able to alert you on scheduled time. " +
                                         "You can however open the app anytime and resume listening.",
                    8);
            }
        }
    }

    public override void OnActivated(UIApplication uiApplication)
    {
        try
        {
            HandleDeliveredNotifications();
            ResetBadgeCount();
        }
        catch (Exception e)
        {
            logger.Error(e, "Error when showing notification on iOS activation.");
        }

        base.OnActivated(uiApplication);
    }

    private void HandleDeliveredNotifications()
    {
        var delivered = UNUserNotificationCenter.Current.GetDeliveredNotificationsAsync().Result;

        if (delivered != null)
        {
            var notification = delivered.FirstOrDefault();

            if (notification != null)
            {
                HandleNotification(notification.Request.Content.UserInfo);
            }

            UNUserNotificationCenter.Current.RemoveAllDeliveredNotifications();
        }
    }

    private void ResetBadgeCount()
    {
        UNUserNotificationCenter.Current.SetBadgeCount(0, error =>
        {
            if (error != null)
            {
                logger.Error("Failed to reset badge count: {Error}", error.LocalizedDescription);
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
            logger.Error(e, "Error handling iOS notification response.");
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
                    logger.Error("Failed to reset badge count: {Error}", error.LocalizedDescription);
                }
            });

            // Extract schedule ID from notification and start playback
            if (userInfo != null && userInfo.TryGetValue(new NSString("ScheduleId"), out var scheduleIdValue))
            {
                var scheduleIdString = scheduleIdValue?.ToString();
                if (!string.IsNullOrEmpty(scheduleIdString) && int.TryParse(scheduleIdString, out var scheduleId) && scheduleId > 0)
                {
                    logger.Information("Notification tapped for schedule {ScheduleId}, starting playback", scheduleId);
                    StartPlaybackFromNotification(scheduleId);
                }
            }
        }
        catch (Exception e)
        {
            logger.Error(e, "Error handling iOS notification.");
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
                    await playbackService.PlayScheduleAsync(scheduleId);
                    logger.Information("Started playback for schedule {ScheduleId} from notification tap", scheduleId);
                }
                else
                {
                    logger.Warning("ISchedulePlaybackService not available for notification playback");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error starting playback from notification for schedule {ScheduleId}", scheduleId);
            }
        });
    }

    public override void PerformFetch(UIApplication application,
        Action<UIBackgroundFetchResult> completionHandler)
    {
        _ = Task.Run(async () =>
        {
            var downloaded = await PerformBackgroundFetchAsync();
            completionHandler(downloaded ? UIBackgroundFetchResult.NewData : UIBackgroundFetchResult.NoData);
        });
    }

    private async Task<bool> PerformBackgroundFetchAsync()
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
            logger.Error(e, "An error occurred in doing perform fetch task.");
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
        TaskScheduler.UnobservedTaskException -= UnobserverdTaskException;

        disposed = true;

        base.Dispose(disposing);
    }
}
