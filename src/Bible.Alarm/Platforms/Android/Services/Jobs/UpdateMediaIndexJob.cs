using Android.App;
using Android.App.Job;
using Android.Content;
using Bible.Alarm.Services;
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

        private static readonly Lazy<Logger> LazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger Logger => LazyLogger.Value;

        public UpdateMediaIndexJob()
        {
            LogSetup.Initialize(VersionFinder.Default,
                new string[] { $"AndroidSdk {Android.OS.Build.VERSION.SdkInt}" }, "Android");
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
            TaskScheduler.UnobservedTaskException += UnobserverdTaskException;
        }

        private void UnobserverdTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Logger.Error(e.Exception, "Unobserved task exception in UpdateMediaIndexJob");
        }

        private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.Fatal(e.ExceptionObject as Exception, "Unhandled exception in UpdateMediaIndexJob");
        }

        public override bool OnStartJob(JobParameters @params)
        {
            Task.Run(async () =>
            {
                try
                {
                    var container = BootstrapHelper.GetInitializedContainer();
                    var mediaIndexService = container.Resolve<MediaIndexService>();
                    await mediaIndexService.UpdateIndexIfAvailable();
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Error updating media index");
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