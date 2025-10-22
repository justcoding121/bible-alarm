using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Mvvm;
using Bible.Alarm.Services.Tasks;
using Bible.Alarm.Services.Infrastructure;
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
        public static void Initialize(ILogger logger)
        {
            // Ensure ServiceProviderManager is initialized for Windows
            EnsureServiceProviderInitialized();
            
            // Setup background tasks
            Task.Run(() => SetupBackgroundTask());

            // Initialize services
            Task.Run(async () =>
            {
                try
                {
                    await CommonBootstrapHelper.VerifyServices();

                    Messenger<bool>.Publish(MvvmMessages.Initialized, true);

                    await Task.Delay(1000);

                    await ServiceProviderManager.GetService<SchedulerTask>().Handle();
                }
                catch (Exception e)
                {
                    logger.Fatal(e, "Windows initialization crashed.");
                }
            });
        }

        /// <summary>
        /// Verify and initialize all services
        /// </summary>
        public static async Task VerifyServices()
        {
            // Ensure ServiceProviderManager is initialized
            EnsureServiceProviderInitialized();
            await CommonBootstrapHelper.VerifyServices();
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
                // Initialize minimal DI container for Windows background services
                // This should only happen if the main app hasn't started yet
                throw new InvalidOperationException(
                    "ServiceProviderManager not initialized. Windows background services require the main app to initialize first.");
            }
        }
    }
}