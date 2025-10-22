using Android.App;
using Android.App.Job;
using Bible.Alarm.Common;
using Bible.Alarm.Droid.Services.Platform;
using Bible.Alarm.Services.Infrastructure;
using Bible.Alarm.Services.Droid.Helpers;
using Bible.Alarm.Services.Media;
using Serilog;

namespace Bible.Alarm.Services.Droid.Tasks;

[Service(Enabled = true)]
public class UpdateMediaIndexJob : JobService
{
    public const int JobId = 2;

    private readonly ILogger _logger;

    public UpdateMediaIndexJob() : this(Log.ForContext<UpdateMediaIndexJob>())
    {
    }

    public UpdateMediaIndexJob(ILogger logger)
    {
        _logger = logger;
        LogSetup.Initialize(VersionFinder.Default,
            new string[] { $"AndroidSdk {Android.OS.Build.VERSION.SdkInt}" }, "Android");
        AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException += UnobserverdTaskException;
    }

    private void UnobserverdTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger.Error(e.Exception, "Unobserved task exception in UpdateMediaIndexJob");
    }

    private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
    {
        _logger.Fatal(e.ExceptionObject as Exception, "Unhandled exception in UpdateMediaIndexJob");
    }

    public override bool OnStartJob(JobParameters @params)
    {
        Task.Run(async () =>
        {
            try
            {
                BootstrapHelper.Initialize(_logger, this);
                await BootstrapHelper.VerifyServices();

                var mediaIndexService = ServiceProviderManager.GetService<MediaIndexService>();
                await mediaIndexService.UpdateIndexIfAvailable();
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error updating media index");
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