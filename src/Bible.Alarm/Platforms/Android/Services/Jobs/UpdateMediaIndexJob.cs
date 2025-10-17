using Android.App;
using Android.App.Job;
using Android.Content;
using Bible.Alarm.Services.Droid.Helpers;
using Bible.Alarm.Droid.Services.Platform;
using Bible.Alarm.Services.Infrastructure;
using NLog;
using System;
using System.Threading.Tasks;

namespace Bible.Alarm.Services.Droid.Tasks
{
    [Service(Enabled = true)]
    public class UpdateMediaIndexJob : JobService
    {
        public const int JobId = 2;

        private static readonly Lazy<Logger> lazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger logger => lazyLogger.Value;

        public UpdateMediaIndexJob()
        {
            LogSetup.Initialize(VersionFinder.Default,
                new string[] { $"AndroidSdk {Android.OS.Build.VERSION.SdkInt}" }, DeviceInfo.Platform.Android);
            AppDomain.CurrentDomain.UnhandledException += unhandledExceptionHandler;
            TaskScheduler.UnobservedTaskException += unobserverdTaskException;
        }

        private void unobserverdTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            logger.Error(e.Exception, "Unobserved task exception in UpdateMediaIndexJob");
        }

        private void unhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            logger.Fatal(e.ExceptionObject as Exception, "Unhandled exception in UpdateMediaIndexJob");
        }

        public override bool OnStartJob(JobParameters @params)
        {
            Task.Run(async () =>
            {
                try
                {
                    var container = BootstrapHelper.GetInitializedContainer();
                    var mediaIndexService = container.Resolve<IMediaIndexService>();
                    await mediaIndexService.UpdateMediaIndex();
                }
                catch (Exception e)
                {
                    logger.Error(e, "Error updating media index");
                }
                finally
                {
                    JobFinished(@params, false);
                }
            });

            return true;
        }

        public override bool OnStopJob(JobParameters @params)
        {
            return false;
        }
    }
}