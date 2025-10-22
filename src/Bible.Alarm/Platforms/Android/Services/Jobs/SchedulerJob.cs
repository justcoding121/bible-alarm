using Android.App;
using Android.App.Job;
using Bible.Alarm.Common;
using Bible.Alarm.Services.Tasks;
using Bible.Alarm.Droid.Services.Platform;
using Bible.Alarm.Services.Infrastructure;
using Bible.Alarm.Services.Droid.Helpers;
using Serilog;

namespace Bible.Alarm.Services.Droid.Tasks;

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
            new string[] { $"AndroidSdk {Android.OS.Build.VERSION.SdkInt}" }, "Android");
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
                BootstrapHelper.Initialize(_logger, this);
                await BootstrapHelper.VerifyServices();
                
                // Container no longer needed - using ServiceProviderManager
                var schedulerService = ServiceProviderManager.GetService<SchedulerTask>();
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