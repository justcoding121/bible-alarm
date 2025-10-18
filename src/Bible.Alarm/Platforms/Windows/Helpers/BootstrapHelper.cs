using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Mvvm;
using Bible.Alarm.Services.Tasks;
using Serilog;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;

namespace Bible.Alarm.Services.Windows.Helpers
{
    public class BootstrapHelper
    {
        public static bool IsBackgroundTaskEnabled = true;

        public static async Task SetupBackgroundTask()
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

        public static void Initialize(ILogger logger)
        {
            Task.Run(() => SetupBackgroundTask());

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
                    logger.Fatal(e, "Uwp initialization crashed.");
                }
            });
        }
    }
}
