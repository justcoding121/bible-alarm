using System.Timers;
using Bible.Alarm.Common;
using Bible.Alarm.Services.Media.Interfaces;
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

    /// <summary>
    /// Gets the MediaElementContainer ContentView. Used by NavigationService to add MediaElement when it's recreated.
    /// </summary>
    public ContentView MediaElementContainerInstance => MediaElementContainer;

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

        // Reattach MediaElement to container when BootstrapPage becomes available
        // This handles the case where MediaElement was created while app was backgrounded
        try
        {
            var mediaElementService = ServiceProviderManager.GetService<IMediaElementService>();
            mediaElementService.ReattachMediaElementIfNeeded();
        }
        catch (Exception ex)
        {
            // Service might not be available yet - that's okay, will retry on next GetMediaElement call
            logger.Warning(ex, "Failed to reattach MediaElement in BootstrapPage.OnAppearing - will retry on next GetMediaElement call");
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

