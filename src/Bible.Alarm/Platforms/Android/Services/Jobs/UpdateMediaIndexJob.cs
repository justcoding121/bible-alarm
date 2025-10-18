using Android.App;
using Android.App.Job;
using Bible.Alarm.Droid.Services.Platform;
using Bible.Alarm.Services.Infrastructure;
using Serilog;

namespace Bible.Alarm.Services.Droid.Tasks;

[Service(Enabled = true)]
public class UpdateMediaIndexJob : JobService
{
    public const int JobId = 2;

    private static readonly ILogger Logger = Log.ForContext<UpdateMediaIndexJob>();

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
                // Container no longer needed - using ServiceProviderManager
                var mediaIndexService = ServiceProviderManager.GetService<MediaIndexService>();
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