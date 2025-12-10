using Bible.Alarm.Common.Helpers;
using Serilog;

namespace Bible.Alarm.Platforms.Windows.Helpers
{
    public class WindowsBootstrapHelper
    {
        public static bool IsBackgroundTaskEnabled = true;

        /// <summary>
        /// Main entry point for Windows platform initialization
        /// </summary>
        public static async Task Initialize(ILogger logger, bool isForeground = false)
        {
            try
            {
                await CommonBootstrapHelper.VerifyServices();
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
            return Task.CompletedTask;

            // Note: For WinUI 3 desktop apps, we'll rely on the main application
            // to handle scheduled tasks rather than system background tasks
            // This is a limitation of moving from UWP to WinUI 3 desktop
        }
    }
}