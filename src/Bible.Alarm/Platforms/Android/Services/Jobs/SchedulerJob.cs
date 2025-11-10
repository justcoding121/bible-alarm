using Android.App;
using Android.App.Job;
using AndroidBuild = global::Android.OS.Build;
using Bible.Alarm;
using Bible.Alarm.Common;
using Bible.Alarm.Services.Scheduler;
using Serilog;
using Bible.Alarm.Platforms.Android.Services.Platform;

namespace Bible.Alarm.Platforms.Android.Services.Jobs;

[Service(Enabled = true)]
public class SchedulerJob : JobService
{
    public const int JobId = 1;

    private readonly ILogger _logger;

    public SchedulerJob() : this(Log.ForContext<SchedulerJob>())
    {
    }

    public SchedulerJob(ILogger logger)
    {
        _logger = logger;
        LogSetup.Initialize(VersionFinder.Default,
            new string[] { $"AndroidSdk {AndroidBuild.VERSION.SdkInt}" }, "Android");
        AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException += UnobserverdTaskException;
    }

    private void UnobserverdTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger.Error(e.Exception, "Unobserved task exception in SchedulerJob");
    }

    private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
    {
        _logger.Fatal(e.ExceptionObject as Exception, "Unhandled exception in SchedulerJob");
    }

    public override bool OnStartJob(JobParameters @params)
    {
        Task.Run(async () =>
        {
            try
            {
                // Initialize DI container for background service
                MauiAppHolder.CreateAndStore();
                // Run bootstrapper after CreateAndStore for background launch
                MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);
                
                var schedulerService = ServiceProviderManager.GetService<SchedulerService>();
                await schedulerService.Handle();
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error processing scheduled tasks");
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