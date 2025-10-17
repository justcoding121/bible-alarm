using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using System;
using NLog;

namespace Bible.Alarm.WinUI
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Microsoft.UI.Xaml.Controls.Page
    {
        private static readonly Lazy<Logger> LazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger Logger => LazyLogger.Value;

        public MainPage()
        {
            InitializeWindow();
        }

        private void InitializeWindow()
        {
            try
            {
                // Windows-specific window styling and initialization
                // In MAUI, window management is handled through the App class
                // This is where you would add Windows-specific window styling code
                // that was typically in the original Xamarin MainPage.xaml.cs
                
                Logger.Info("Windows MainPage initialized with platform-specific styling.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error initializing Windows MainPage window properties.");
            }
        }
    }
}
