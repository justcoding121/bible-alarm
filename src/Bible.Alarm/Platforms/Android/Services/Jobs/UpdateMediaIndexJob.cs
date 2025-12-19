using Android.App;
using Android.App.Job;
using Bible.Alarm.Common;
using Bible.Alarm.Platforms.Android.Services.Platform;
using Bible.Alarm.Services.Media.Interfaces;
using Serilog;
using AndroidBuild = Android.OS.Build;

namespace Bible.Alarm.Platforms.Android.Services.Jobs;

[Service(Enabled = true, Permission = "android.permission.BIND_JOB_SERVICE")]
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
        LogSetup.Initialize(AndroidVersionFinder.Default,
            [$"AndroidSdk {AndroidBuild.VERSION.SdkInt}"], "Android");
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
                // Initialize DI container for background service
                MauiAppHolder.CreateAndStore();
                // Run bootstrapper after CreateAndStore for background launch
                MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);

                // Wait for bootstrap to complete before using database services
                await MauiProgram.WaitForBootstrapAsync();

                // IMediaIndexService is a singleton, so don't dispose it
                var mediaIndexService = ServiceProviderManager.GetService<IMediaIndexService>();
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
