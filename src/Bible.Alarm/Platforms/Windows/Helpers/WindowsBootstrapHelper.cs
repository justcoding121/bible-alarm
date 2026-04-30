using Bible.Alarm.Common.Helpers;
using Serilog;

namespace Bible.Alarm.Platforms.Windows.Helpers;

public static class WindowsBootstrapHelper
{
    public static bool IsBackgroundTaskEnabled { get; set; } = true;

    /// <summary>
    /// Main entry point for Windows platform initialization
    /// </summary>
    public static async Task Initialize(ILogger logger, bool isForeground = false)
    {
        try
        {
            // Pass isForeground to VerifyServices so InitializedMessage is sent for foreground launches
            // Database operations run in background Task.Run, so UI thread is not blocked
            await CommonBootstrapHelper.VerifyServices(isForeground);
            logger.Information("Windows database initialization completed successfully.");
        }
        catch (Exception e)
        {
            // CommonBootstrapHelper.VerifyServices already handles exceptions internally,
            // but if an exception escapes (e.g., from SetupBackgroundTask), log it without crashing
            logger.Error(e, "Windows database initialization encountered an error (non-fatal).");
            // Don't re-throw - allow app to continue running even if bootstrap has issues
        }

        // Fire-and-forget: SetupBackgroundTask runs synchronously and completes immediately
        _ = Task.Run(SetupBackgroundTask);
    }

    private static Task SetupBackgroundTask()
    {
        // For WinUI 3 desktop apps, we can't use UWP background tasks
        // Instead, we'll use a different approach for scheduled tasks
        // Background execution is generally available for desktop apps
        IsBackgroundTaskEnabled = true;

        // Note: Media index update background task handler is available at:
        // This can be called from Windows Task Scheduler or app lifecycle events
        // WinUI 3 doesn't support UWP background tasks the same way as UWP

        return Task.CompletedTask;
    }
}
