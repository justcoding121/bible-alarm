using Bible.Alarm.Common;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Database;
using Bible.Alarm.Models;
using Bible.Alarm.Platforms.iOS.Services.Platform;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Scheduler;
using Foundation;
using Serilog;
using UIKit;
using UserNotifications;

namespace Bible.Alarm.Platforms.iOS
{
    // The UIApplicationDelegate for the application. This class is responsible for launching the 
    // User Interface of the application, as well as listening (and optionally responding) to 
    // application events from iOS.
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate, IUNUserNotificationCenterDelegate
    {
        private static readonly ILogger Logger = Log.ForContext<AppDelegate>();

        public AppDelegate()
        {
            // Initialize logging and exception handling
            LogSetup.Initialize(iOSVersionFinder.Default, [], "iOS");
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
            TaskScheduler.UnobservedTaskException += UnobserverdTaskException;
        }

        private void UnobserverdTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Logger.Error(e.Exception, "Unobserved task exception.");
        }

        private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.Error("Unhandled exception.", e.SerializeObject());
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
                Logger.Fatal(e, "iOS MAUI app creation failed.");
                throw;
            }
        }

        public override bool FinishedLaunching(UIApplication app, NSDictionary launchOptions)
        {

#if DEBUG
            // Note: SSL certificate validation for localhost is handled by HttpClient configuration
            // This is now managed through HttpClientHandler in the HTTP client setup
#endif

            try
            {
                // BootstrapHelper is already initialized in MauiProgram.cs
                // No need to call it again here for foreground scenarios
                
                //once every hour
                // Note: Background fetch is now handled by BGAppRefreshTask in iOS 13+
                if (UIDevice.CurrentDevice.CheckSystemVersion(13, 0))
                {
                    // Use BGAppRefreshTask for iOS 13+
                    // This is configured in the app's Info.plist
                }
                else
                {
                    UIApplication.SharedApplication.SetMinimumBackgroundFetchInterval(60 * 60);
                }
            }
            catch (Exception e)
            {
                Logger.Fatal(e, "iOS application crashed.");
                throw;
            }

            // check for a notification
            if (launchOptions != null)
            {
                try
                {
                    // check for a local notification
                    // CA1422: UIApplication.LaunchOptionsLocalNotificationKey is obsolete but needed for compatibility
                    // Validate platform compatibility
#pragma warning disable CA1422
                    if (launchOptions.ContainsKey(UIApplication.LaunchOptionsLocalNotificationKey))
                    {
                        if (launchOptions[UIApplication.LaunchOptionsLocalNotificationKey] is UILocalNotification localNotification)
                        {
                            HandleNotification(localNotification.UserInfo);
                        }
                    }
#pragma warning restore CA1422
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Error handling iOS notification on launch.");
                }
            }

            // Request notification permissions from the user
            UNUserNotificationCenter.Current.RequestAuthorization(
                UNAuthorizationOptions.Alert
                | UNAuthorizationOptions.Sound
                | UNAuthorizationOptions.Badge, (approved, _) =>
                {
                    if (!approved)
                    {
                        Task.Run(() =>
                        {
                            try
                            {
                                // Ensure MauiApp is created (may already be created from FinishedLaunching)
                                MauiAppHolder.CreateAndStore();
                                // Run bootstrapper after CreateAndStore for background launch
                                MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);
                                
                                using var dbContext = ServiceProviderManager.GetService<ScheduleDbContext>();

                                if (!dbContext.GeneralSettings.Any(x => x.Key == "iOSNotificationDisabledMsgShown"))
                                {
                                    dbContext.GeneralSettings.Add(new GeneralSettings
                                    {
                                        Key = "iOSNotificationDisabledMsgShown",
                                        Value = "true"
                                    });
                                    dbContext.SaveChanges();

                                    var popupService = ServiceProviderManager.GetService<IToastService>();
                                    popupService.ShowMessage("You've disabled notifications. " +
                                                             "We won't be able to alert you on scheduled time. " +
                                                             "You can however open the app anytime and resume listening.",
                                        8);
                                }
                            }
                            catch (Exception e)
                            {
                                Logger.Error(e, "Error when prompting iOS notification permission on launch.");
                            }
                        });
                    }
                });

            return base.FinishedLaunching(app, launchOptions);
        }

        public override void OnActivated(UIApplication uiApplication)
        {
            try
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

#pragma warning disable CA1422
                UIApplication.SharedApplication.ApplicationIconBadgeNumber = 0;
#pragma warning restore CA1422
            }
            catch (Exception e)
            {
                Logger.Error(e, "Error when showing notification on iOS activation.");
            }

            base.OnActivated(uiApplication);
        }


        private static void HandleNotification(NSDictionary _)
        {
            try
            {
                // reset our badge
#pragma warning disable CA1422
                UIApplication.SharedApplication.ApplicationIconBadgeNumber = 0;
#pragma warning restore CA1422
            }
            catch (Exception e)
            {
                Logger.Error(e, "Error handling iOS notification.");
            }
        }

        public async override void PerformFetch(UIApplication application,
            Action<UIBackgroundFetchResult> completionHandler)
        {
            var downloaded = false;

            try
            {
                // Ensure MauiApp is created exactly once (thread-safe)
                // This is the iOS background fetch entry point
                MauiAppHolder.CreateAndStore();
                // Run bootstrapper after CreateAndStore for background launch
                MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);

                using var schedulerService = ServiceProviderManager.GetService<SchedulerService>();
                downloaded = await schedulerService.HandleAsync();

                var mediaIndexService = ServiceProviderManager.GetService<MediaIndexService>();
                downloaded = downloaded || await mediaIndexService.UpdateIndexIfAvailable();
                // Note: mediaIndexService is a singleton and should not be disposed
            }
            catch (Exception e)
            {
                Logger.Error(e, "An error occurred in doing perform fetch task.");
            }

            // Inform system of fetch results
            completionHandler(downloaded ? UIBackgroundFetchResult.NewData : UIBackgroundFetchResult.NoData);
        }

        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            AppDomain.CurrentDomain.UnhandledException -= UnhandledExceptionHandler;
            TaskScheduler.UnobservedTaskException -= UnobserverdTaskException;

            _disposed = true;

            base.Dispose(disposing);
        }
    }
}