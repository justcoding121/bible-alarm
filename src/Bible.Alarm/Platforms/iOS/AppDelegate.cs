#nullable enable

using BackgroundTasks;
using Bible.Alarm.Common;
using Bible.Alarm.Platforms.iOS.Services.BackgroundTasks;
using Bible.Alarm.Platforms.iOS.Services.Platform;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Foundation;
using Serilog;
using UIKit;
using UserNotifications;

namespace Bible.Alarm.Platforms.iOS;

// The UIApplicationDelegate for the application. This class is responsible for launching the 
// User Interface of the application, as well as listening (and optionally responding) to 
// application events from iOS.
[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate, IUNUserNotificationCenterDelegate
{
    private static readonly ILogger logger = Log.ForContext<AppDelegate>();

    public AppDelegate()
    {
        // Initialize logging and exception handling
        LogSetup.Initialize(IOsVersionFinder.Default, [], "iOS");
        AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException += UnobserverdTaskException;
    }

    private void UnobserverdTaskException(object? sender, UnobservedTaskExceptionEventArgs e) => logger.Error(e.Exception, "Unobserved task exception.");

    private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
    {
        logger.Error(e.ExceptionObject as Exception, "Unhandled exception. IsTerminating: {IsTerminating}",
            e.IsTerminating);
    }

    protected override MauiApp CreateMauiApp()
    {
        try
        {
            // Ensure MauiApp is created exactly once (thread-safe)
            // Foreground launch - bootstrap will run on background Task
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
#if DEBUG
        // Note: SSL certificate validation for localhost is handled by HttpClient configuration
        // This is now managed through HttpClientHandler in the HTTP client setup
#endif

        try
        {
            SetupBackgroundTasks();
        }
        catch (Exception e)
        {
            logger.Fatal(e, "iOS application crashed.");
            throw;
        }

        SetupNotifications();

        return base.FinishedLaunching(app, launchOptions);
    }

    private void SetupBackgroundTasks()
    {
        // BootstrapHelper is already initialized in MauiProgram.cs
        // No need to call it again here for foreground scenarios

        //once every hour
        // Note: Background fetch is now handled by BGAppRefreshTask in iOS 13+
        if (UIDevice.CurrentDevice.CheckSystemVersion(13, 0))
        {
            // Use BGTaskScheduler for iOS 13+ to register background tasks
            // Media index update is handled separately from scheduler
            RegisterMediaIndexUpdateBackgroundTask();
            // Schedule the initial background task run
            ScheduleMediaIndexUpdateBackgroundTask();
        }
        else
        {
            UIApplication.SharedApplication.SetMinimumBackgroundFetchInterval(60 * 60);
        }
    }

    private void SetupNotifications()
    {
        // Set the notification center delegate to handle notification taps (including app launch from notification)
        // This is the modern, non-deprecated way to handle notifications in iOS 15+
        UNUserNotificationCenter.Current.Delegate = this;

        // Request notification permissions from the user
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
        // Ensure MauiApp is created (may already be created from FinishedLaunching)
        MauiAppHolder.CreateAndStore();
        // Run bootstrapper after CreateAndStore for background launch
        MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);

        // Wait for bootstrap to complete before using database services
        await MauiProgram.WaitForBootstrapAsync();

        // Use GeneralSettingsService to check and set the setting
        var generalSettingsService = ServiceProviderManager.GetService<IGeneralSettingsService>();
        if (generalSettingsService != null)
        {
            if (!await generalSettingsService.GeneralSettingExistsAsync("iOSNotificationDisabledMsgShown"))
            {
                await generalSettingsService.SetGeneralSettingAsync(
                    "iOSNotificationDisabledMsgShown",
                    "true");

                // IToastService is a singleton, so don't dispose it
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
        // In 2025, SetBadgeCount is the standard for 95%+ of the iOS market (iOS 16+)
        // This avoids CA1422 entirely and uses the modern asynchronous pattern.
        UNUserNotificationCenter.Current.SetBadgeCount(0, error =>
        {
            if (error != null)
            {
                logger.Error("Failed to reset badge count: {Error}", error.LocalizedDescription);
            }
            else
            {
                logger.Information("Badge count reset successfully.");
            }
        });
    }

    /// <summary>
    /// Called when the user taps a notification (including when app is launched from notification).
    /// This is the modern, non-deprecated way to handle notification interactions in iOS 15+.
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
        // Allow the notification to be presented even when app is in foreground
        // Use Banner instead of deprecated Alert option
        var options = UNNotificationPresentationOptions.Banner | UNNotificationPresentationOptions.Sound | UNNotificationPresentationOptions.Badge;
        completionHandler(options);
    }

    private static void HandleNotification(NSDictionary userInfo)
    {
        try
        {
            // reset our badge
            // In 2025, SetBadgeCount is the standard for 95%+ of the iOS market (iOS 16+)
            // This avoids CA1422 entirely and uses the modern asynchronous pattern.
            UNUserNotificationCenter.Current.SetBadgeCount(0, error =>
            {
                if (error != null)
                {
                    logger.Error("Failed to reset badge count: {Error}", error.LocalizedDescription);
                }
                else
                {
                    logger.Information("Badge count reset successfully.");
                }
            });
        }
        catch (Exception e)
        {
            logger.Error(e, "Error handling iOS notification.");
        }
    }

    public override void PerformFetch(UIApplication application,
        Action<UIBackgroundFetchResult> completionHandler)
    {
        // Use Task.Run to handle async operations without async void
        // The base class requires void return type, so we can't use async Task
        _ = Task.Run(async () =>
        {
            var downloaded = await PerformBackgroundFetchAsync();
            // Inform system of fetch results
            completionHandler(downloaded ? UIBackgroundFetchResult.NewData : UIBackgroundFetchResult.NoData);
        });
    }

    private async Task<bool> PerformBackgroundFetchAsync()
    {
        var downloaded = false;

        try
        {
            // Ensure MauiApp is created exactly once (thread-safe)
            // This is the iOS background fetch entry point for scheduler only
            // Media index update is handled separately via UpdateMediaIndexBackgroundTask
            MauiAppHolder.CreateAndStore();
            // Run bootstrapper after CreateAndStore for background launch
            MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);

            // Wait for bootstrap to complete before using database services
            await MauiProgram.WaitForBootstrapAsync();

            // ISchedulerService is a singleton, so don't dispose it
            var schedulerService = ServiceProviderManager.GetService<ISchedulerService>();
            downloaded = await schedulerService.HandleAsync();
        }
        catch (Exception e)
        {
            logger.Error(e, "An error occurred in doing perform fetch task.");
        }

        return downloaded;
    }

    private const string MediaIndexUpdateTaskIdentifier = "com.jthomas.info.Bible.Alarm.MediaIndexUpdate";

    /// <summary>
    /// Registers the media index update background task with BGTaskScheduler for iOS 13+
    /// </summary>
    private void RegisterMediaIndexUpdateBackgroundTask()
    {
        try
        {
            BGTaskScheduler.Shared.Register(MediaIndexUpdateTaskIdentifier, null, task =>
            {
                if (task is BGAppRefreshTask refreshTask)
                {
                    HandleMediaIndexUpdateBackgroundTask(refreshTask);
                }
            });
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to register media index update background task");
        }
    }

    /// <summary>
    /// Handles the media index update background task when triggered by the system
    /// </summary>
    private void HandleMediaIndexUpdateBackgroundTask(BGAppRefreshTask task)
    {
        if (task == null)
        {
            return;
        }

        task.ExpirationHandler = () =>
        {
            logger.Warning("Media index update background task expired");
        };

        Task.Run(async () =>
        {
            try
            {
                var downloaded = await UpdateMediaIndexBackgroundTask.HandleAsync();
                task.SetTaskCompleted(downloaded);
            }
            catch (Exception e)
            {
                logger.Error(e, "Error in media index update background task");
                task.SetTaskCompleted(false);
            }
        });
    }

    /// <summary>
    /// Schedules the next media index update background task
    /// This should be called after the task completes to schedule the next run
    /// </summary>
    public static void ScheduleMediaIndexUpdateBackgroundTask()
    {
        try
        {
            var request = new BGAppRefreshTaskRequest(MediaIndexUpdateTaskIdentifier)
            {
                EarliestBeginDate = NSDate.FromTimeIntervalSinceNow(60 * 60 * 24) // Schedule for 24 hours from now
            };

            BGTaskScheduler.Shared.Submit(request, out var error);
            if (error != null)
            {
                Log.ForContext<AppDelegate>().Warning("Failed to schedule media index update background task: {Error}", error);
            }
        }
        catch (Exception e)
        {
            Log.ForContext<AppDelegate>().Error(e, "Error scheduling media index update background task");
        }
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
