#nullable enable
using Bible.Alarm.Views;
using Bible.Alarm.Views.General;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using Bible.Alarm.Common;

namespace Bible.Alarm.Services.UI;

public class WindowSetupService(IServiceProvider serviceProvider)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private static NavigationPage? _mainNavPage;

    public Window CreateWindow(IActivationState? activationState)
    {
        var bootstrapPage = _serviceProvider.GetRequiredService<BootstrapPage>();
        
        var navigationPage = new NavigationPage(bootstrapPage);
        
        // Initialize centralized navigation bar color management
        Initialize(navigationPage);
        
        // NavigationPage background will adapt to theme via BootstrapPage
        NavigationPage.SetHasNavigationBar(bootstrapPage, false);

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
        catch
        {
            // Fallback to theme colors if resource lookup fails
            var theme = ThemeColors.GetCurrentTheme();
            _mainNavPage.BarBackgroundColor = ThemeColors.CardBackground.Get(theme);
            _mainNavPage.BarTextColor = ThemeColors.PrimaryText.Get(theme);
        }
    }
}

