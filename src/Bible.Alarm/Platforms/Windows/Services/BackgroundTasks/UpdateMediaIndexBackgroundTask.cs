using Bible.Alarm.Common;
using Bible.Alarm.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Platforms.Windows.Services.BackgroundTasks;

/// <summary>
/// Windows background task handler for updating media index
/// Note: WinUI 3 doesn't support UWP background tasks the same way as UWP.
/// This handler can be called from scheduled tasks or app lifecycle events.
/// </summary>
public class UpdateMediaIndexBackgroundTask
{
    private static readonly ILogger Logger = Log.ForContext<UpdateMediaIndexBackgroundTask>();

    public static async Task<bool> HandleAsync()
    {
        try
        {
            // Ensure MauiApp is created exactly once (thread-safe)
            // This is the Windows background task entry point
            MauiAppHolder.CreateAndStore();
            // Run bootstrapper after CreateAndStore for background launch
            MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);

            // Wait for bootstrap to complete before using database services
            await MauiProgram.WaitForBootstrapAsync();

            // IMediaIndexService is a singleton, so don't dispose it
            var mediaIndexService = ServiceProviderManager.GetService<IMediaIndexService>();
            return await mediaIndexService.UpdateIndexIfAvailable();
        }
        catch (Exception e)
        {
            Logger.Error(e, "Error updating media index in Windows background task");
            return false;
        }
    }
}

