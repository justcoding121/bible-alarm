using CommunityToolkit.Mvvm.Messaging;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.UI.Messenger;
using Bible.Alarm.Services.Tasks;
using Serilog;
// Removed UWP background task APIs - not available in WinUI 3 desktop apps

namespace Bible.Alarm.Platforms.Windows.Helpers
{
    public class BootstrapHelper
    {
        public static bool IsBackgroundTaskEnabled = true;

        /// <summary>
        /// Main entry point for Windows platform initialization
        /// </summary>
        public static void Initialize(ILogger logger, bool isForeground = false)
        {
            // Ensure ServiceProviderManager is initialized for Windows
            EnsureServiceProviderInitialized();
            
            // Initialize database and services (both foreground and background)
            // This must complete before any other tasks
            try
            {
                CommonBootstrapHelper.VerifyServices().Wait();
                logger.Information("Windows database initialization completed successfully.");
            }
            catch (Exception e)
            {
                logger.Fatal(e, "Windows database initialization crashed.");
                throw;
            }
            
            // Setup background tasks
            Task.Run(SetupBackgroundTask);

            // Initialize UI components only for foreground scenarios
            if (isForeground)
            {
                InitializeUi(logger);
            }
        }

        private static void InitializeUi(ILogger logger)
        {
            // UI-specific initialization only
            Task.Run(async () =>
            {
                try
                {
                    WeakReferenceMessenger.Default.Send(new InitializedMessage(true));

                    await Task.Delay(1000);

                    await ServiceProviderManager.GetService<SchedulerTask>().Handle();
                }
                catch (Exception e)
                {
                    logger.Fatal(e, "Windows UI initialization crashed.");
                }
            });
        }


        private static Task SetupBackgroundTask()
        {
            // For WinUI 3 desktop apps, we can't use UWP background tasks
            // Instead, we'll use a different approach for scheduled tasks
            // Background execution is generally available for desktop apps
            IsBackgroundTaskEnabled = true;
            return Task.CompletedTask;

            // Note: For WinUI 3 desktop apps, we'll rely on the main application
            // to handle scheduled tasks rather than system background tasks
            // This is a limitation of moving from UWP to WinUI 3 desktop
        }

        private static void RegisterMediaIndexUpdateTask()
        {
            // For WinUI 3 desktop apps, background tasks are not available
            // This functionality would need to be implemented using alternative approaches
            // such as Windows Task Scheduler or a background service
            // For now, we'll skip this registration
        }

        private static void RegisterSchedulerTask()
        {
            // For WinUI 3 desktop apps, background tasks are not available
            // This functionality would need to be implemented using alternative approaches
            // such as Windows Task Scheduler or a background service
            // For now, we'll skip this registration
        }


        private static void EnsureServiceProviderInitialized()
        {
            if (ServiceProviderManager.IsInitialized) return;
            // Initialize DI container for background services
            var logger = Log.ForContext<BootstrapHelper>();
            logger.Information("Initializing DI container for background service...");
                
            // Call MauiProgram to ensure DI container is initialized
            MauiProgram.EnsureDiContainerInitialized();
                
            logger.Information("DI container initialized for background service.");
        }
    }
}