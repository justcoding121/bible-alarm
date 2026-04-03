#nullable enable
using Bible.Alarm.Views.Shared;
using Serilog;

#if ANDROID
using AndroidX.Core.View;
using Android.Views;
using View = Android.Views.View;
#endif

namespace Bible.Alarm.Views;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class BaseContentPage : ContentPage
{
    public BaseContentPage()
    {
        InitializeComponent();

        ControlTemplate = new ControlTemplate(() =>
        {
            // Two-row Grid: content fills Row 0 (*), mini bar occupies Row 1 (Auto).
            // When the mini bar is hidden (IsVisible=false) the Auto row collapses to zero
            // so content fills the full height. When visible, content is pushed up and
            // nothing is hidden behind the bar.
            var grid = new Grid
            {
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill,
                RowDefinitions =
                {
                    new RowDefinition(GridLength.Star),
                    new RowDefinition(GridLength.Auto)
                }
            };

            var presenter = new ContentPresenter
            {
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill
            };
            Grid.SetRow(presenter, 0);
            grid.Add(presenter);

            var miniBar = new MiniPlaybackBar();
            Grid.SetRow(miniBar, 1);
            grid.Add(miniBar);

            return grid;
        });
    }

    /// <summary>
    /// When true the page applies top padding to stay below the Android status bar.
    /// Override and return false for immersive pages (e.g. the playback modal) that
    /// intentionally draw behind the status bar.
    /// </summary>
    protected virtual bool ApplyAndroidSafeAreaPadding => true;

#if ANDROID
    private static readonly ILogger logger = Log.ForContext<BaseContentPage>();
    private View? _trackedPlatformView;
    private readonly int[] _windowLocation = new int[2];
    private double _appliedSafeAreaTop;

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        DetachSafeAreaListener();

        if (!ApplyAndroidSafeAreaPadding)
        {
            return;
        }

        if (Handler?.PlatformView is View platformView
            && platformView.ViewTreeObserver != null)
        {
            _trackedPlatformView = platformView;
            platformView.ViewTreeObserver.GlobalLayout += OnSafeAreaLayoutCheck;
        }
    }

    private void DetachSafeAreaListener()
    {
        if (_trackedPlatformView != null)
        {
            try
            {
                var observer = _trackedPlatformView.ViewTreeObserver;
                if (observer is { IsAlive: true })
                {
                    observer.GlobalLayout -= OnSafeAreaLayoutCheck;
                }
            }
            catch (ObjectDisposedException)
            {
            }
        }

        _trackedPlatformView = null;
    }

    /// <summary>
    /// On Android API 35+ (edge-to-edge), MAUI's NavigationPage can lose the status bar
    /// inset offset after certain layout cycles (e.g. CollectionView item height changes).
    /// This listener detects when the page content is behind the status bar and applies
    /// compensating padding. When the native offset is working, no extra padding is added.
    /// </summary>
    private void OnSafeAreaLayoutCheck(object? sender, EventArgs e)
    {
        try
        {
            if (_trackedPlatformView == null
                || _trackedPlatformView.Width == 0
                || _trackedPlatformView.Height == 0)
            {
                return;
            }

            var activity = Platform.CurrentActivity;
            var decorView = activity?.Window?.DecorView;
            if (decorView == null)
            {
                return;
            }

            var rootInsets = ViewCompat.GetRootWindowInsets(decorView);
            if (rootInsets == null)
            {
                return;
            }

            var statusBars = rootInsets.GetInsets(WindowInsetsCompat.Type.StatusBars());
            if (statusBars == null)
            {
                return;
            }

            int statusBarHeightPx = statusBars.Top;
            if (statusBarHeightPx <= 0)
            {
                return;
            }

            _trackedPlatformView.GetLocationInWindow(_windowLocation);
            int viewTopPx = _windowLocation[1];

            float density = activity!.Resources?.DisplayMetrics?.Density ?? 1f;
            double neededDip = Math.Max(0, (statusBarHeightPx - viewTopPx) / (double)density);

            if (Math.Abs(_appliedSafeAreaTop - neededDip) > 0.5)
            {
                _appliedSafeAreaTop = neededDip;
                Dispatcher.Dispatch(() =>
                {
                    Padding = new Thickness(Padding.Left, neededDip, Padding.Right, Padding.Bottom);
                });
            }
        }
        catch (ObjectDisposedException)
        {
            DetachSafeAreaListener();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Safe area layout check failed");
        }
    }
#endif
}
