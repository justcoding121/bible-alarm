#nullable enable
using Bible.Alarm.ViewModels.Shared;
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
            // Two-row Grid: content fills Row 0 (*), mini bar occupies Row 1.
            // When playback is active we already know the bar height from the previous page;
            // use a fixed row height so Row 1 does not start at zero and expand after measure
            // (which reads as a flicker on push). When the bar is hidden, Auto collapses the row.
            var miniVm = MiniPlaybackBarViewModel.Instance;
            var barRow = miniVm != null
                && miniVm.IsVisible
                && MiniPlaybackBar.LastMeasuredHeight > 0
                ? new RowDefinition(new GridLength(MiniPlaybackBar.LastMeasuredHeight, GridUnitType.Absolute))
                : new RowDefinition(GridLength.Auto);

            var grid = new Grid
            {
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill,
                RowDefinitions =
                {
                    new RowDefinition(GridLength.Star),
                    barRow
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
    private const double SafeAreaPaddingChangeThresholdDip = 3.0;

    private View? _trackedPlatformView;
    private readonly int[] _windowLocation = new int[2];
    private double _appliedSafeAreaTop;

    private static int _cachedStatusBarHeightPx;

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

            ApplyInitialSafeAreaPadding();

            if (platformView.ViewTreeObserver is { IsAlive: true })
            {
                platformView.ViewTreeObserver.GlobalLayout += OnSafeAreaLayoutCheck;
            }

            platformView.Post(() => PostApplySafeAreaAfterLayout(useNoiseThreshold: false));
        }
    }

    /// <summary>
    /// Best-effort padding before the first native layout pass (view may not have a
    /// stable window position yet). A <see cref="View.Post"/> callback and
    /// GlobalLayout apply the same inset formula once coordinates are reliable.
    /// </summary>
    private void ApplyInitialSafeAreaPadding()
    {
        try
        {
            var activity = Platform.CurrentActivity;
            if (activity == null)
            {
                return;
            }

            int statusBarHeightPx = _cachedStatusBarHeightPx;

            if (statusBarHeightPx <= 0)
            {
                statusBarHeightPx = GetStatusBarHeightFromWindowMetrics(activity);
            }

            if (statusBarHeightPx <= 0)
            {
                statusBarHeightPx = GetStatusBarHeightFromViewCompat(activity);
            }

            if (statusBarHeightPx <= 0)
            {
                return;
            }

            _cachedStatusBarHeightPx = statusBarHeightPx;

            float density = activity.Resources?.DisplayMetrics?.Density ?? 1f;
            double neededDip = statusBarHeightPx / (double)density;

            _appliedSafeAreaTop = neededDip;
            Padding = new Thickness(Padding.Left, neededDip, Padding.Right, Padding.Bottom);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to apply initial safe area padding");
        }
    }

    private void PostApplySafeAreaAfterLayout(bool useNoiseThreshold)
    {
        try
        {
            TryUpdateSafeAreaPaddingFromInsets(useNoiseThreshold);
        }
        catch (ObjectDisposedException)
        {
            DetachSafeAreaListener();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "PostApplySafeAreaAfterLayout failed");
        }
    }

    private void TryUpdateSafeAreaPaddingFromInsets(bool useNoiseThreshold = true)
    {
        if (_trackedPlatformView == null
            || _trackedPlatformView.Width == 0
            || _trackedPlatformView.Height == 0)
        {
            return;
        }

        int statusBarHeightPx = _cachedStatusBarHeightPx;

        if (statusBarHeightPx <= 0)
        {
            var activity = Platform.CurrentActivity;
            var decorView = activity?.Window?.DecorView;
            if (decorView == null)
            {
                return;
            }

            var rootInsets = ViewCompat.GetRootWindowInsets(decorView);
            var statusBars = rootInsets?.GetInsets(WindowInsetsCompat.Type.StatusBars());
            statusBarHeightPx = statusBars?.Top ?? 0;

            if (statusBarHeightPx > 0)
            {
                _cachedStatusBarHeightPx = statusBarHeightPx;
            }
        }

        if (statusBarHeightPx <= 0)
        {
            return;
        }

        _trackedPlatformView.GetLocationInWindow(_windowLocation);
        int viewTopPx = _windowLocation[1];

        float density = (Platform.CurrentActivity?.Resources?.DisplayMetrics?.Density) ?? 1f;
        double neededDip = Math.Max(0, (statusBarHeightPx - viewTopPx) / (double)density);

        double threshold = useNoiseThreshold ? SafeAreaPaddingChangeThresholdDip : 0.5;
        if (Math.Abs(_appliedSafeAreaTop - neededDip) > threshold)
        {
            _appliedSafeAreaTop = neededDip;
            Padding = new Thickness(Padding.Left, neededDip, Padding.Right, Padding.Bottom);
        }
    }

    /// <summary>
    /// WindowManager.CurrentWindowMetrics is available as soon as the Activity window exists,
    /// even before the first layout pass — unlike ViewCompat.GetRootWindowInsets which
    /// may return null until insets are dispatched.
    /// </summary>
    private static int GetStatusBarHeightFromWindowMetrics(Android.App.Activity activity)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            return 0;
        }

        try
        {
            var metrics = activity.WindowManager?.CurrentWindowMetrics;
            if (metrics == null)
            {
                return 0;
            }

            var insets = metrics.WindowInsets.GetInsetsIgnoringVisibility(
                WindowInsets.Type.StatusBars() | WindowInsets.Type.DisplayCutout());
            return insets.Top;
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "WindowMetrics status bar height query failed");
            return 0;
        }
    }

    private static int GetStatusBarHeightFromViewCompat(Android.App.Activity activity)
    {
        try
        {
            var decorView = activity.Window?.DecorView;
            if (decorView == null)
            {
                return 0;
            }

            var rootInsets = ViewCompat.GetRootWindowInsets(decorView);
            var statusBars = rootInsets?.GetInsets(WindowInsetsCompat.Type.StatusBars());
            return statusBars?.Top ?? 0;
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "ViewCompat status bar height query failed");
            return 0;
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
            TryUpdateSafeAreaPaddingFromInsets(useNoiseThreshold: true);
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
