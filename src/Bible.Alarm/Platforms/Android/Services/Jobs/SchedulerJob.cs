using Android.App;
using Android.App.Job;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Constants;
using Serilog;
using AndroidBuild = Android.OS.Build;

namespace Bible.Alarm.Platforms.Android.Services.Jobs;

[Service(Enabled = true, Permission = "android.permission.BIND_JOB_SERVICE")]
public class SchedulerJob : JobService
{
    public const int JobId = 1;

    private readonly ILogger logger;

    public SchedulerJob() : this(Log.ForContext<SchedulerJob>())
    {
    }

    public SchedulerJob(ILogger logger)
    {
        this.logger = logger;
        LogSetup.Initialize(new AssemblyAppVersionFinder(),
            [$"AndroidSdk {AndroidBuild.VERSION.SdkInt}"], AppConstants.Platform.Android);
        AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException += UnobserverdTaskException;
    }

    private const int CrashFlushDelayMs = 500;

    private void UnobserverdTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        logger.Error(e.Exception, AppConstants.Logging.ProcessDiagnosticsLog.UnobservedTaskExceptionInSchedulerJob);
        FlushAndDelay();
    }

    private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
    {
        logger.Fatal(e.ExceptionObject as Exception, AppConstants.Logging.ProcessDiagnosticsLog.UnhandledExceptionInSchedulerJob);
        FlushAndDelay();
    }

    private static void FlushAndDelay()
    {
        try
        {
            Log.CloseAndFlush();
        }
        catch
        {
        }

        Thread.Sleep(CrashFlushDelayMs);
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

                // Wait for bootstrap to complete before using database services
                await MauiProgram.WaitForBootstrapAsync();

                // ISchedulerService is a singleton, so don't dispose it
                var schedulerService = ServiceProviderManager.GetService<ISchedulerService>();
                await schedulerService.HandleAsync();
            }
            catch (Exception e)
            {
                logger.Error(e, "Error processing scheduled tasks");
            }
            finally
            {
                JobFinished(@params, false);
            }
        });

        return true;
    }

    public override bool OnStopJob(JobParameters @params) => false;
}
