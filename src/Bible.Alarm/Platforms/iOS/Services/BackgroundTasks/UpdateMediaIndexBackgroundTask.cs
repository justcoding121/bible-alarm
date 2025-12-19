using Bible.Alarm.Common;
using Bible.Alarm.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Platforms.iOS.Services.BackgroundTasks;

/// <summary>
/// iOS background task handler for updating media index
/// This is used with BGTaskScheduler for iOS 13+ (minimum OS version is 15.0)
/// </summary>
public class UpdateMediaIndexBackgroundTask
{
    private static readonly ILogger Logger = Log.ForContext<UpdateMediaIndexBackgroundTask>();

    public static async Task<bool> HandleAsync()
    {
        try
        {
            // Ensure MauiApp is created exactly once (thread-safe)
            // This is the iOS background task entry point
            MauiAppHolder.CreateAndStore();
            // Run bootstrapper after CreateAndStore for background launch
            MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);

            // Wait for bootstrap to complete before using database services
            await MauiProgram.WaitForBootstrapAsync();

            // IMediaIndexService is a singleton, so don't dispose it
            var mediaIndexService = ServiceProviderManager.GetService<IMediaIndexService>();
            var result = await mediaIndexService.UpdateIndexIfAvailable();

            // Schedule the next background task run
            AppDelegate.ScheduleMediaIndexUpdateBackgroundTask();

            return result;
        }
        catch (Exception e)
        {
            Logger.Error(e, "Error updating media index in iOS background task");
            return false;
        }
    }
}

