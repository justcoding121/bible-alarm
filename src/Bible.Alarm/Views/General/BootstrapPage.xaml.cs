using System.Timers;
using Timer = System.Timers.Timer;

namespace Bible.Alarm.Views.General;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class BootstrapPage : ContentPage, IDisposable
{
    private bool _isDisposed;
    private readonly Timer _animationTimer;
    private int _currentDot;

    public BootstrapPage()
    {
        InitializeComponent();
        
        // Start animated dots
        // Change dot every 500ms
        _animationTimer = new Timer(500);
        _animationTimer.Elapsed += OnTimerElapsed;
        _animationTimer.AutoReset = true;
        _animationTimer.Start();
        
        // Initialize first dot
        UpdateDots();
    }


    private void OnTimerElapsed(object sender, ElapsedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _currentDot = (_currentDot + 1) % 3;
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
        switch (_currentDot)
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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _animationTimer?.Stop();
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _animationTimer?.Stop();
            _animationTimer?.Dispose();
            _isDisposed = true;
        }
    }
}

