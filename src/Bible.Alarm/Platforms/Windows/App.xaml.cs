using Bible.Alarm.Services.Infrastructure;
using NLog;
using System;

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
            // Initialize MAUI application
            // MAUI will handle the App instantiation through MauiProgram
            // Platform-specific initialization can be done here if needed
        }
    }
}
