using Bible.Alarm.Services.Infrastructure;
using Serilog;
using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Microsoft.UI;

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
                // Note: Exit event handling is different in MAUI
                
                // Initialize Windows-specific services
                // Note: Container initialization is handled by MAUI framework
                Logger.Information("Windows application initialized successfully.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error initializing Windows-specific components.");
            }
        }

        // Note: Exit handling is managed by MAUI framework
    }
}
