using Bible.Alarm;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Mvvm.Messenger;
using Bible.Alarm.Services.Tasks;
using Serilog;
using Windows.ApplicationModel.Background;

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
            Task.Run(() => SetupBackgroundTask());

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
                    Messenger<bool>.Publish(MvvmMessages.Initialized, true);

                    await Task.Delay(1000);

                    await ServiceProviderManager.GetService<SchedulerTask>().Handle();
                }
                catch (Exception e)
                {
                    logger.Fatal(e, "Windows UI initialization crashed.");
                }
            });
        }


        private static async Task SetupBackgroundTask()
        {
            await BackgroundExecutionManager.RequestAccessAsync();
            var allowed = BackgroundExecutionManager.GetAccessStatus();
            switch (allowed)
            {
                case BackgroundAccessStatus.AllowedSubjectToSystemPolicy:
                case BackgroundAccessStatus.AlwaysAllowed:
                    break;
                case BackgroundAccessStatus.Unspecified:
                case BackgroundAccessStatus.DeniedBySystemPolicy:
                case BackgroundAccessStatus.DeniedByUser:
                    IsBackgroundTaskEnabled = false;
                    break;
            }

            RegisterSchedulerTask();
            RegisterMediaIndexUpdateTask();
        }

        private static void RegisterMediaIndexUpdateTask()
        {
            var registered = false;

            foreach (var cur in BackgroundTaskRegistration.AllTasks)
            {
                if (cur.Value.Name == "MediaIndexUpdateTask")
                {
                    registered = true;
                }
            }

            if (!registered)
            {
                var builder = new BackgroundTaskBuilder
                {
                    Name = "MediaIndexUpdateTask"
                };

                builder.SetTrigger(new TimeTrigger(60, true));
                builder.Register();
            }
        }

        private static void RegisterSchedulerTask()
        {
            var registered = false;

            foreach (var cur in BackgroundTaskRegistration.AllTasks)
            {
                if (cur.Value.Name == "SchedulerTask")
                {
                    registered = true;
                }
            }

            if (!registered)
            {
                var builder = new BackgroundTaskBuilder
                {
                    Name = "SchedulerTask"
                };

                builder.SetTrigger(new TimeTrigger(15, true));
                builder.Register();
            }
        }


        private static void EnsureServiceProviderInitialized()
        {
            if (!ServiceProviderManager.IsInitialized)
            {
                // Initialize DI container for background services
                var logger = Log.ForContext<BootstrapHelper>();
                logger.Information("Initializing DI container for background service...");
                
                // Call MauiProgram to ensure DI container is initialized
                MauiProgram.EnsureDiContainerInitialized();
                
                logger.Information("DI container initialized for background service.");
            }
        }
    }
}