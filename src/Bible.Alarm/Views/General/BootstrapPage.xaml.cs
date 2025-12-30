using System.Timers;
using Bible.Alarm.Common;
using Bible.Alarm.Services.UI.Interfaces;
using Serilog;
using Timer = System.Timers.Timer;

namespace Bible.Alarm.Views.General;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class BootstrapPage : ContentPage, IDisposable
{
    private static readonly ILogger logger = Log.ForContext<BootstrapPage>();
    private bool isDisposed;
    private readonly Timer animationTimer;
    private int currentDot;
    private bool hasNavigatedToHome;

    public BootstrapPage()
    {
        InitializeComponent();

        // Set theme-aware background color
        this.SetAppThemeColor(BackgroundColorProperty, ThemeColors.Bootstrap.LightBackground, ThemeColors.Bootstrap.DarkBackground);

        // Set theme-aware text colors for dots
        var lightColor = ThemeColors.Primary.SlateBlue;
        var darkColor = ThemeColors.Primary.LightPurpleForDark;
        Dot1.SetAppThemeColor(Label.TextColorProperty, lightColor, darkColor);
        Dot2.SetAppThemeColor(Label.TextColorProperty, lightColor, darkColor);
        Dot3.SetAppThemeColor(Label.TextColorProperty, lightColor, darkColor);

        // Start animated dots
        // Change dot every 500ms
        animationTimer = new Timer(500);
        animationTimer.Elapsed += OnTimerElapsed;
        animationTimer.AutoReset = true;
        animationTimer.Start();

        // Initialize first dot
        UpdateDots();
    }


    private void OnTimerElapsed(object sender, ElapsedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            currentDot = (currentDot + 1) % 3;
            UpdateDots();
        });
    }

    private void UpdateDots()
    {
        // Reset all dots to low opacity
        Dot1.Opacity = 0.3;
        Dot2.Opacity = 0.3;
        Dot3.Opacity = 0.3;

        // Highlight current dot
        switch (currentDot)
        {
            case 0:
                Dot1.Opacity = 1.0;
                break;
            case 1:
                Dot2.Opacity = 1.0;
                break;
            case 2:
                Dot3.Opacity = 1.0;
                break;
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Navigate to Home immediately on first appearance to eliminate delay
        // Home page will show progress bar while bootstrap completes
        if (!hasNavigatedToHome)
        {
            hasNavigatedToHome = true;
            _ = Task.Run(async () =>
            {
                try
                {
                    // Small delay to ensure BootstrapPage is fully rendered
                    await Task.Delay(50);
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        try
                        {
                            var navigationService = ServiceProviderManager.GetService<INavigationService>();
                            await navigationService.NavigateToHomeAsync();
                            logger.Information("BootstrapPage: Navigated to Home immediately");
                        }
                        catch (Exception ex)
                        {
                            logger.Warning(ex, "BootstrapPage: Failed to navigate to Home immediately, will wait for InitializedMessage");
                        }
                    });
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "BootstrapPage: Error in immediate navigation to Home");
                }
            });
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        animationTimer?.Stop();
    }

    public void Dispose()
    {
        if (!isDisposed)
        {
            animationTimer?.Stop();
            animationTimer?.Dispose();
            isDisposed = true;
        }
    }
}

