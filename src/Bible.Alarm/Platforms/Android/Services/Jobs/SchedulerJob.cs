using Android.App;
using Android.App.Job;
using Android.Content;
using Bible.Alarm.Services.Droid.Helpers;
using Bible.Alarm.Services.Tasks;
using Bible.Alarm.Droid.Services.Platform;
using Bible.Alarm.Services.Infrastructure;
using Serilog;
using System;
using System.Threading.Tasks;

namespace Bible.Alarm.Services.Droid.Tasks
{
    [Service(Enabled = true)]
    public class SchedulerJob : JobService
    {
        public const int JobId = 1;

        private static readonly ILogger Logger = Log.ForContext<SchedulerJob>();

        public SchedulerJob()
        {
            LogSetup.Initialize(VersionFinder.Default,
                new string[] { $"AndroidSdk {Android.OS.Build.VERSION.SdkInt}" }, "Android");
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
            TaskScheduler.UnobservedTaskException += UnobserverdTaskException;
        }

        private void UnobserverdTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Logger.Error(e.Exception, "Unobserved task exception in SchedulerJob");
        }

        private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.Fatal(e.ExceptionObject as Exception, "Unhandled exception in SchedulerJob");
        }

        public override bool OnStartJob(JobParameters @params)
        {
            Task.Run(async () =>
            {
                try
                {
                    var container = BootstrapHelper.GetInitializedContainer();
                    var schedulerService = container.Resolve<SchedulerTask>();
                    await schedulerService.Handle();
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Error processing scheduled tasks");
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