#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Interfaces.Platform;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Serilog;

#if ANDROID
using Microsoft.Maui.Platform;
#endif

namespace Bible.Alarm.Services.UI;

public sealed class WindowSetupService(IServiceProvider serviceProvider, IPlaybackModalService playbackModalService, INavigationService navigationService) : IWindowSetupService, IDisposable
{
    private static NavigationPage? mainNavPage;
    private static readonly ILogger logger = Log.ForContext<WindowSetupService>();
    private bool isDisposed;

    // Base window dimensions (phone-like portrait layout)
    // Must accommodate content across all pages:
    // - Home: margins(32) + time(80) + play(56) + days(140) + nav(80) = 388px
    // - Schedule details: margins(32) + 7 day buttons(7×48=336) + spacing(24) = 392px  
    // - Alarm modal: margins(48) + 5 media buttons(48+48+64+48+48=256) + spacing(60) = 364px
    // - Selection pages: margins(32) + text(200) + icon(40) = 272px
    // Widest is ~400px, add padding for comfort = 450px base
    private const double BaseWidth = 450;
    private const double BaseHeight = 850;
    // ~1.9:1 aspect ratio (phone-like)
    private const double AspectRatio = BaseHeight / BaseWidth;

    public Window CreateWindow(IActivationState? activationState)
    {
        var navigationPage = serviceProvider.GetRequiredService<NavigationPage>();
        Initialize(navigationPage);
        var window = new Window(navigationPage);

#if WINDOWS
        var (width, height, minWidth, minHeight) = CalculateWindowSize();
        window.Width = width;
        window.Height = height;
#if !DEBUG
        window.MinimumWidth = minWidth;
        window.MinimumHeight = minHeight;
#endif
#endif

        navigationService.ClearCache();

        Task.Run(async () =>
        {
            try
            {
                await CommonBootstrapHelper.VerifyServices(true);
            }
            catch (Exception ex)
            {
                // VerifyServices runs in background - failures are non-critical, app continues to work
                logger.Warning(ex, "Error in VerifyServices background task");
            }
        });

        playbackModalService.SubscribeToPlaybackStateChanges();

        return window;
    }

    /// <summary>
    /// Initializes centralized navigation bar color management.
    /// </summary>
    private static void Initialize(NavigationPage navigationPage)
    {
        mainNavPage = navigationPage;
        Application.Current!.RequestedThemeChanged += OnRequestedThemeChanged;
        UpdateNavigationBarColors();
    }

    private static void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e) => UpdateNavigationBarColors();

    /// <summary>
    /// Updates NavigationPage bar colors and status bar appearance from theme-aware resources.
    /// </summary>
    public static void UpdateNavigationBarColors()
    {
        if (mainNavPage == null)
        {
            return;
        }

        try
        {
            if (Application.Current?.Resources.TryGetValue("CardBackgroundColor", out var cardBgColor) == true &&
                cardBgColor is Color cardBg)
            {
                mainNavPage.BarBackgroundColor = cardBg;
            }
            else
            {
                var theme = ThemeColors.GetCurrentTheme();
                mainNavPage.BarBackgroundColor = ThemeColors.CardBackground.Get(theme);
            }

            if (Application.Current?.Resources.TryGetValue("PrimaryTextColor", out var primaryTextColor) == true &&
                primaryTextColor is Color primaryText)
            {
                mainNavPage.BarTextColor = primaryText;
            }
            else
            {
                var theme = ThemeColors.GetCurrentTheme();
                mainNavPage.BarTextColor = ThemeColors.PrimaryText.Get(theme);
            }

            UpdateStatusBarAppearance();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error updating navigation bar colors, using fallback theme colors");
            var theme = ThemeColors.GetCurrentTheme();
            mainNavPage.BarBackgroundColor = ThemeColors.CardBackground.Get(theme);
            mainNavPage.BarTextColor = ThemeColors.PrimaryText.Get(theme);
        }
    }

    /// <summary>
    /// Sets the status bar icon/text color to match the current theme.
    /// Light mode: dark icons. Dark mode: light icons.
    /// </summary>
    private static void UpdateStatusBarAppearance()
    {
        try
        {
            var isLightTheme = ThemeColors.GetCurrentTheme() == AppTheme.Light;

#if ANDROID
            var activity = Platform.CurrentActivity;
            var window = activity?.Window;
            if (window != null)
            {
                // Set status bar background to match page background (obsolete on API 35+; we skip via runtime check).
#pragma warning disable CA1422
                if ((int)Android.OS.Build.VERSION.SdkInt < 35)
                {
                    var statusBarColor = isLightTheme
                        ? ThemeColors.Background.Light
                        : ThemeColors.Background.Dark;
                    window.SetStatusBarColor(statusBarColor.ToPlatform());
                }
#pragma warning restore CA1422

                // Use WindowInsetsControllerCompat for reliable status bar icon appearance
                var decorView = window.DecorView;
                if (decorView != null)
                {
                    var controller = AndroidX.Core.View.WindowCompat.GetInsetsController(window, decorView);
                    if (controller != null)
                    {
                        // AppearanceLightStatusBars = true means dark icons (for light backgrounds)
                        controller.AppearanceLightStatusBars = isLightTheme;
                    }
                }
            }
#elif IOS
            // UIViewControllerBasedStatusBarAppearance is set to false in Info.plist,
            // so we use the app-level API; PreferredStatusBarStyle would require per–view-controller setup.
#pragma warning disable CA1422
            UIKit.UIApplication.SharedApplication.SetStatusBarStyle(
                isLightTheme ? UIKit.UIStatusBarStyle.DarkContent : UIKit.UIStatusBarStyle.LightContent,
                animated: true);
#pragma warning restore CA1422
#endif
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Error updating status bar appearance");
        }
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        if (Application.Current != null)
        {
            Application.Current.RequestedThemeChanged -= OnRequestedThemeChanged;
        }
    }

    public void TearDown()
    {
        playbackModalService.UnsubscribeToPlaybackStateChanges();
        navigationService.PopAllModalsAndPages();

        mainNavPage = null;

        var playbackService = serviceProvider.GetService<IPlaybackService>();

        if (playbackService != null)
        {
            logger.Information("TearDown - Calling player dismiss action");

            Task.Run(async () =>
            {
                try
                {
                    await playbackService.StopAsync();
                    logger.Information("TearDown - Player dismiss action completed");
                }
                catch (Exception ex)
                {
                    // Disposal errors are non-critical (cleanup operation)
                    logger.Warning(ex, "Error in StopAsync or disposal");
                }
            });
        }
    }

#if WINDOWS
    /// <summary>
    /// Calculates optimal window size based on screen dimensions and accessibility settings.
    /// Creates a phone-like portrait window that scales with OS font size and fits on screen.
    /// Considers content requirements across all pages:
    /// - Home: time, play button, 7 day indicators, nav buttons
    /// - Schedule details: 7 day selector buttons
    /// - Alarm modal: 5 media control buttons
    /// </summary>
    private (double Width, double Height, double MinWidth, double MinHeight) CalculateWindowSize()
    {
        try
        {
            // Get OS accessibility font scale factor
            var accessibilityService = serviceProvider.GetService<IAccessibilityFontScaleService>();
            double fontScale = accessibilityService?.FontScale ?? 1.0;

            // Get screen dimensions
            var displayInfo = DeviceDisplay.MainDisplayInfo;
            double screenWidth = displayInfo.Width / displayInfo.Density;
            double screenHeight = displayInfo.Height / displayInfo.Density;

            // Calculate minimum width needed based on font scale
            // Consider widest content across all pages:
            //
            // Home page schedule items:
            //   Margins(32) + Time(80) + Play(56) + Days(140) + Nav(80) + Spacing(50)
            //
            // Schedule details page (7 day selector buttons):
            //   Margins(32) + 7 buttons(7×48=336) + Spacing(24)
            //
            // Alarm modal (5 media control buttons):
            //   Margins(48) + 5 buttons(256) + Spacing(60)
            //
            double homePageWidth = 32 + (80 + 56 + 140 + 80) * fontScale + 50;
            double scheduleDetailsWidth = 32 + (48 * 7) * fontScale + 24;
            double alarmModalWidth = 48 + (48 + 48 + 64 + 48 + 48) * fontScale + 60;
            
            double contentBasedMinWidth = Math.Max(homePageWidth, Math.Max(scheduleDetailsWidth, alarmModalWidth));
            
            // Use the larger of base width or content-based minimum
            double effectiveMinWidth = Math.Max(BaseWidth, contentBasedMinWidth);
            double effectiveMinHeight = effectiveMinWidth * AspectRatio;

            // Scale target dimensions by accessibility factor
            double scaledWidth = BaseWidth * fontScale;
            double scaledHeight = BaseHeight * fontScale;

            // Use the larger of scaled size or effective minimum
            scaledWidth = Math.Max(scaledWidth, effectiveMinWidth);
            scaledHeight = Math.Max(scaledHeight, effectiveMinHeight);

            // Reserve space for taskbar and window chrome (approx 100px vertical, 50px horizontal)
            double maxUsableHeight = screenHeight - 100;
            double maxUsableWidth = screenWidth - 50;

            // Constrain to fit on screen while maintaining aspect ratio
            if (scaledHeight > maxUsableHeight)
            {
                scaledHeight = maxUsableHeight;
                scaledWidth = scaledHeight / AspectRatio;
            }

            if (scaledWidth > maxUsableWidth)
            {
                scaledWidth = maxUsableWidth;
                scaledHeight = scaledWidth * AspectRatio;
            }

            // Final dimensions
            double width = scaledWidth;
            double height = scaledHeight;

            // Minimum dimensions also scale with font size to prevent content clipping
            double minWidth = Math.Min(effectiveMinWidth, maxUsableWidth);
            double minHeight = Math.Min(effectiveMinHeight, maxUsableHeight);

            logger.Debug("Window size calculated: {Width:F0}x{Height:F0}, min: {MinWidth:F0}x{MinHeight:F0} (screen: {ScreenWidth:F0}x{ScreenHeight:F0}, fontScale: {FontScale:F2})",
                width, height, minWidth, minHeight, screenWidth, screenHeight, fontScale);

            return (width, height, minWidth, minHeight);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error calculating window size, using defaults");
            return (BaseWidth, BaseHeight, BaseWidth, BaseHeight);
        }
    }
#endif
}
