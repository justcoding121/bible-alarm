﻿using Android.App;
using Android.App.Job;
using Android.OS;
using Bible.Alarm.Droid.Services.Platform;
using Bible.Alarm.Services.Droid.Helpers;
using Bible.Alarm.Services.Droid.Tasks;
using Bible.Alarm.Services.Infrastructure;
using Bible.Alarm.Services.Tasks;
using NLog;
using System;
using System.Threading.Tasks;

namespace Bible.Alarm.Droid.Services.Tasks
{
    [Service(Name = "com.jthomas.info.Bible.Alarm.jobscheduler.SchedulerJob",
         Permission = "android.permission.BIND_JOB_SERVICE")]
    public class SchedulerJob : JobService, IDisposable
    {
        public const int JobId = 1;
        private IContainer container;
        private static readonly Lazy<Logger> lazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger logger => lazyLogger.Value;

        public SchedulerJob()
        {
            LogSetup.Initialize(VersionFinder.Default,
           new string[] { $"AndroidSdk {Build.VERSION.SdkInt}" }, Xamarin.Forms.Device.Android);
            AppDomain.CurrentDomain.UnhandledException += unhandledExceptionHandler;
            TaskScheduler.UnobservedTaskException += unobserverdTaskException;
        }

        private void unobserverdTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            logger.Error("Unobserved task exception.", e.Exception);
        }

        private void unhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            logger.Error("Unhandled exception.", e);
        }

        public override bool OnStartJob(JobParameters jobParams)
        {

            Task.Run(async () =>
            {
                try
                {
                    this.container = BootstrapHelper.InitializeService(this);

                    await BootstrapHelper.VerifyServices(container);

                    using var schedulerTask = container.Resolve<SchedulerTask>();
                    await schedulerTask.Handle();
                }
                catch (Exception e)
                {
                    logger.Error(e, "An error happened when handling the repeating scheduler task.");
                }

                // Have to tell the JobScheduler the work is done. 
                JobFinished(jobParams, false);
            });

            // Return true because of the asynchronous work
            return true;
        }

        public override bool OnStopJob(JobParameters jobParams)
        {
            // we don't want to reschedule the job 
            // if it is stopped or cancelled.
            return false;
        }

        private bool disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            container = null;
            BootstrapHelper.Remove(this);

            AppDomain.CurrentDomain.UnhandledException -= unhandledExceptionHandler;
            TaskScheduler.UnobservedTaskException -= unobserverdTaskException;

            disposed = true;

            base.Dispose(disposing);
        }
    }
}