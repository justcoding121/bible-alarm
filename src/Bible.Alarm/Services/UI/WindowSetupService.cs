#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Views;
using Bible.Alarm.Views.General;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Serilog;

namespace Bible.Alarm.Services.UI;

public class WindowSetupService(IServiceProvider serviceProvider, IAlarmModalService alarmModalService, INavigationService navigationService) : IWindowSetupService, IDisposable
{
    private static NavigationPage? _mainNavPage;
    private static readonly ILogger Logger = Log.ForContext<WindowSetupService>();
    private bool _isDisposed;

    public Window CreateWindow(IActivationState? activationState)
    {
        // Get NavigationPage from service provider (registered as singleton)
        var navigationPage = serviceProvider.GetRequiredService<NavigationPage>();

        // Initialize centralized navigation bar color management
        Initialize(navigationPage);

        var window = new Window(navigationPage);

#if WINDOWS
        // Set window size preferences
        // Increased height to ensure media metadata (Title, SubTitle, Description) is not cut off by playback controls
        window.Width = 400;
        window.Height = 800;
#if !DEBUG
        // Set minimum dimensions only in release mode
        // In debug mode, allow free resizing for testing
        window.MinimumWidth = 400;
        window.MinimumHeight = 800;
#endif
#endif

        navigationService.ClearCache();

        Logger.Information("WindowSetupService.CreateWindow: Starting VerifyServices with initializeUI=true");
        Task.Run(async () =>
        {
            try
            {
                await CommonBootstrapHelper.VerifyServices(true);
                Logger.Information("WindowSetupService.CreateWindow: VerifyServices completed");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "WindowSetupService.CreateWindow: Error in VerifyServices");
            }
        });

        alarmModalService.SubscribeToPlaybackStateChanges();

        return window;
    }

    /// <summary>
    /// Initializes centralized navigation bar color management.
    /// This ensures the navigation bar updates automatically when the theme changes.
    /// </summary>
    private static void Initialize(NavigationPage navigationPage)
    {
        _mainNavPage = navigationPage;
        Application.Current!.RequestedThemeChanged += OnRequestedThemeChanged;
        UpdateNavigationBarColors();
    }

    /// <summary>
    /// Handles theme changes and updates navigation bar colors.
    /// </summary>
    private static void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        UpdateNavigationBarColors();
    }

    /// <summary>
    /// Updates NavigationPage bar colors from theme-aware resources.
    /// NavigationPage properties don't support DynamicResource directly, so we update them programmatically.
    /// This is called automatically on theme changes.
    /// </summary>
    public static void UpdateNavigationBarColors()
    {
        if (_mainNavPage == null) return;

        try
        {
            // Read from Application resources - these are updated by App.xaml.cs on theme change
            if (Application.Current?.Resources.TryGetValue("CardBackgroundColor", out var cardBgColor) == true &&
                cardBgColor is Color cardBg)
            {
                _mainNavPage.BarBackgroundColor = cardBg;
            }
            else
            {
                // Fallback if resource not found
                var theme = ThemeColors.GetCurrentTheme();
                _mainNavPage.BarBackgroundColor = ThemeColors.CardBackground.Get(theme);
            }

            if (Application.Current?.Resources.TryGetValue("PrimaryTextColor", out var primaryTextColor) == true &&
                primaryTextColor is Color primaryText)
            {
                _mainNavPage.BarTextColor = primaryText;
            }
            else
            {
                // Fallback if resource not found
                var theme = ThemeColors.GetCurrentTheme();
                _mainNavPage.BarTextColor = ThemeColors.PrimaryText.Get(theme);
            }
        }
        catch (Exception ex)
        {
            // Fallback to theme colors if resource lookup fails
            Logger.Warning(ex, "Error updating navigation bar colors, using fallback theme colors");
            var theme = ThemeColors.GetCurrentTheme();
            _mainNavPage.BarBackgroundColor = ThemeColors.CardBackground.Get(theme);
            _mainNavPage.BarTextColor = ThemeColors.PrimaryText.Get(theme);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        // Unsubscribe from theme changes
        if (Application.Current != null)
        {
            Application.Current.RequestedThemeChanged -= OnRequestedThemeChanged;
        }
    }

    public void TearDown()
    {
        alarmModalService.UnsubscribeToPlaybackStateChanges();
        navigationService.PopAllModalsAndPages();

        // Clear static reference to NavigationPage to prevent memory leaks
        // The old NavigationPage will be garbage collected once all references are cleared
        _mainNavPage = null;

        var playbackService = serviceProvider.GetService<IPlaybackService>();

        if (playbackService != null)
        {
            Logger.Information("MainActivity.OnDestroy - Calling player dismiss action");

            Task.Run(async () =>
            {
                try
                {
                    await playbackService.StopAsync();
                    Logger.Information("MainActivity.OnDestroy - Player dismiss action completed");

                    // Dispose MauiApp after StopAsync completes
                    Logger.Information("MainActivity.OnDestroy - MauiApp disposed");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error in StopAsync or disposal");
                }
            });
        }
    }
}

