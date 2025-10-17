using Bible.Alarm.Services.Infrastructure;
using NLog;
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
        private static readonly Lazy<Logger> lazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger logger => lazyLogger.Value;

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
                logger.Info("Windows application initialized successfully.");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error initializing Windows-specific components.");
            }
        }

        // Note: Exit handling is managed by MAUI framework
    }
}
