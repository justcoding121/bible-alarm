using Serilog;

namespace Bible.Alarm.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Microsoft.UI.Xaml.Application
    {
        private static readonly ILogger Logger = Log.ForContext<App>();

        public App()
        {
            InitializeWindowsSpecific();
        }

        private void InitializeWindowsSpecific()
        {
            try
            {
                // Windows-specific initialization
                // Set up window management
                // MAUI handles application lifecycle events automatically

                // Initialize Windows-specific services
                // Dependency injection container is configured in MauiProgram
                Logger.Information("Windows application initialized successfully.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error initializing Windows-specific components.");
            }
        }

        // Application lifecycle is managed by MAUI framework
    }
}