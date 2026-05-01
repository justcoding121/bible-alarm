#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Interfaces.Platform;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores.Actions.Playback;
using Serilog;

#if ANDROID
using Microsoft.Maui.Platform;
#endif

namespace Bible.Alarm.Services.UI;

public sealed partial class WindowSetupService(IServiceProvider serviceProvider, IPlaybackModalService playbackModalService, INavigationService navigationService) : IWindowSetupService
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
    // Widest is ~400px, add padding for comfort = 450px base (used by Windows window sizing only)
#if WINDOWS
    private const double BaseWidth = 450;
    private const double BaseHeight = 850;
#endif

    public Window CreateWindow(IActivationState? activationState)
    {
        // When a new window is created the app will be visible. On Android, after a swipe-out
        // the old activity's OnStop set IsInForeground=false, but MAUI may not call OnStart/OnResume
        // again on the Application for the recreated activity, leaving the flag stale.
        App.MarkInForegroundAfterWindowCreated();

        // Resolve the singleton early so MiniPlaybackBarViewModel.Instance is set
        // before Home's ControlTemplate (containing MiniPlaybackBar) materializes.
        serviceProvider.GetRequiredService<ViewModels.Shared.MiniPlaybackBarViewModel>();

        var navigationPage = serviceProvider.GetRequiredService<NavigationPage>();
        Initialize(navigationPage);

        var window = new Window(navigationPage);
        window.Activated += OnWindowFirstActivated;

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

#if ANDROID
        window.Created += (_, _) =>
        {
            try
            {
                var barHost = serviceProvider.GetService<Platforms.Android.Services.UI.Interfaces.IAndroidMiniPlaybackBarHost>();
                barHost?.Attach();
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error attaching AndroidMiniPlaybackBarHost");
            }
        };
#endif

        return window;
    }

    /// <summary>
    /// Clears the static NavigationPage reference during teardown (isolated static mutation).
    /// </summary>
    private static void ClearMainNavigationPageReference()
    {
        mainNavPage = null;
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

    private void OnWindowFirstActivated(object? sender, EventArgs e)
    {
        if (sender is Window w)
        {
            w.Activated -= OnWindowFirstActivated;
            UpdateNavigationBarColors();
        }
    }

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
            if (Application.Current?.Resources.TryGetValue("CardBackgroundColor", out var cardBgColor) is true &&
                cardBgColor is Color cardBg)
            {
                mainNavPage.BarBackgroundColor = cardBg;
            }
            else
            {
                var theme = ThemeColors.GetCurrentTheme();
                mainNavPage.BarBackgroundColor = ThemeColors.CardBackground.Get(theme);
            }

            if (Application.Current?.Resources.TryGetValue("PrimaryTextColor", out var primaryTextColor) is true &&
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
    /// Sets the status bar and navigation bar colors and icon appearance to match the current theme.
    /// Light mode: dark icons on light backgrounds. Dark mode: light icons on dark backgrounds.
    /// </summary>
    private static void UpdateStatusBarAppearance()
    {
        try
        {
#if ANDROID || IOS
            var isLightTheme = ThemeColors.GetCurrentTheme() == AppTheme.Light;
#endif

#if ANDROID
            var activity = Platform.CurrentActivity;
            var window = activity?.Window;
            if (window != null)
            {
                var barColor = isLightTheme
                    ? ThemeColors.Background.Light
                    : ThemeColors.Background.Dark;
                var platformColor = barColor.ToPlatform();

#pragma warning disable CA1422
                if ((int)Android.OS.Build.VERSION.SdkInt < 35)
                {
                    window.SetStatusBarColor(platformColor);
                    window.SetNavigationBarColor(platformColor);
                }
#pragma warning restore CA1422

                // On API 35+ (edge-to-edge), status/navigation bars are transparent and show the
                // window background behind them. Since ConfigChanges.UiMode prevents Activity recreation,
                // the XML theme's windowBackground never reloads on theme change. Update it here so
                // the area behind transparent system bars matches the current theme.
                window.DecorView?.SetBackgroundColor(platformColor);

                var decorView = window.DecorView;
                if (decorView != null)
                {
                    var controller = AndroidX.Core.View.WindowCompat.GetInsetsController(window, decorView);
                    if (controller != null)
                    {
                        // true = dark icons (for light backgrounds), false = light icons (for dark backgrounds)
                        controller.AppearanceLightStatusBars = isLightTheme;
                        controller.AppearanceLightNavigationBars = isLightTheme;
                    }
                }
            }
#elif IOS
            // Scene-based apps ignore the deprecated UIApplication.SetStatusBarStyle.
            // With UIViewControllerBasedStatusBarAppearance = true, UIKit queries the topmost
            // view controller's PreferredStatusBarStyle. UINavigationController derives that
            // from NavigationBar.BarStyle, so we set it explicitly and trigger a re-query.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    var rootVc = GetRootViewController();

                    if (rootVc is UIKit.UINavigationController navController)
                    {
#pragma warning disable CA1422
                        navController.NavigationBar.BarStyle = isLightTheme
                            ? UIKit.UIBarStyle.Default
                            : UIKit.UIBarStyle.Black;
#pragma warning restore CA1422
                    }

                    var topVc = GetTopmostViewController(rootVc);
                    topVc?.SetNeedsStatusBarAppearanceUpdate();

                    logger.Debug("[STATUS-BAR] iOS: Updated BarStyle & SetNeedsStatusBarAppearanceUpdate (isLightTheme={IsLight})", isLightTheme);
                }
                catch (Exception ex)
                {
                    logger.Debug(ex, "[STATUS-BAR] iOS: status bar update failed");
                }
            });
#endif
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Error updating status bar appearance");
        }
    }

#if IOS
    private static UIKit.UIViewController? GetRootViewController()
    {
        var scene = UIKit.UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIKit.UIWindowScene>()
            .FirstOrDefault();
        return scene?.KeyWindow?.RootViewController;
    }

    private static UIKit.UIViewController? GetTopmostViewController(UIKit.UIViewController? vc)
    {
        while (vc?.PresentedViewController != null)
        {
            vc = vc.PresentedViewController;
        }

        return vc;
    }
#endif

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

        ClearMainNavigationPageReference();

        // Dispatch PlaybackStoppedAction synchronously AFTER unsubscribing so Fluxor state
        // is clean (Stopped) before a new session starts. The handler is already unsubscribed,
        // so no UI close logic fires (which would fail during activity destruction).
        // The background StopForTeardownAsync skips dispatching to prevent stale actions
        // from interfering with a new session after activity recreation.
        var dispatcher = serviceProvider.GetService<Fluxor.IDispatcher>();
        dispatcher?.Dispatch(new PlaybackStoppedAction());
#if ANDROID || IOS
        dispatcher?.Dispatch(new SetCarPlayScreenAction());
#endif

        var playbackService = serviceProvider.GetService<IPlaybackService>();

        if (playbackService != null)
        {
            logger.Information("TearDown - Calling player teardown stop");

            Task.Run(async () =>
            {
                try
                {
                    await playbackService.StopForTeardownAsync();
                    logger.Information("TearDown - Player teardown stop completed");
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "Error in StopForTeardownAsync");
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
            double effectiveMinHeight = effectiveMinWidth * (BaseHeight / BaseWidth);

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
                scaledWidth = scaledHeight / (BaseHeight / BaseWidth);
            }

            if (scaledWidth > maxUsableWidth)
            {
                scaledWidth = maxUsableWidth;
                scaledHeight = scaledWidth * (BaseHeight / BaseWidth);
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
