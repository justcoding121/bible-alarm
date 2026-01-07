#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.UI;

public sealed class WindowSetupService(IServiceProvider serviceProvider, IAlarmModalService alarmModalService, INavigationService navigationService) : IWindowSetupService, IDisposable
{
    private static NavigationPage? mainNavPage;
    private static readonly ILogger logger = Log.ForContext<WindowSetupService>();
    private bool isDisposed;

    public Window CreateWindow(IActivationState? activationState)
    {
        var navigationPage = serviceProvider.GetRequiredService<NavigationPage>();

        Initialize(navigationPage);

        var window = new Window(navigationPage);

#if WINDOWS
        window.Width = 400;
        window.Height = 800;
#if !DEBUG
        window.MinimumWidth = 400;
        window.MinimumHeight = 800;
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
                logger.Error(ex, "Error in VerifyServices");
            }
        });

        alarmModalService.SubscribeToPlaybackStateChanges();

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
    /// Updates NavigationPage bar colors from theme-aware resources.
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
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error updating navigation bar colors, using fallback theme colors");
            var theme = ThemeColors.GetCurrentTheme();
            mainNavPage.BarBackgroundColor = ThemeColors.CardBackground.Get(theme);
            mainNavPage.BarTextColor = ThemeColors.PrimaryText.Get(theme);
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
        alarmModalService.UnsubscribeToPlaybackStateChanges();
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
                    logger.Error(ex, "Error in StopAsync or disposal");
                }
            });
        }
    }
}
