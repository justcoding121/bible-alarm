#nullable enable
using Bible.Alarm.ViewModels;
using Serilog;
using Syncfusion.Maui.Buttons;

namespace Bible.Alarm.Views;

public partial class Home : BaseContentPage, IDisposable
{
    private bool _isDisposed;
    private bool _hasHandledFirstLoad;
    private readonly HomeViewModel _viewModel;

    public Home(HomeViewModel vm)
    {
        var constructorStartTime = DateTime.UtcNow;
        Log.Information("[PERF] Home page: Constructor started at {StartTime}", constructorStartTime);

        var initComponentStartTime = DateTime.UtcNow;
        InitializeComponent();
        var initComponentElapsed = (DateTime.UtcNow - initComponentStartTime).TotalMilliseconds;
        Log.Information("[PERF] Home page: InitializeComponent took {ElapsedMs}ms", initComponentElapsed);

        BindingContext = vm;
        _viewModel = vm;

        // Use Loaded event which fires after the page is in the visual tree
        Loaded += OnPageLoaded;

        var constructorElapsed = (DateTime.UtcNow - constructorStartTime).TotalMilliseconds;
        Log.Information("[PERF] Home page: Constructor completed in {ElapsedMs}ms", constructorElapsed);
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        // Only handle once per page instance
        if (_hasHandledFirstLoad)
        {
            return;
        }

        _hasHandledFirstLoad = true;

        // Unsubscribe to avoid multiple calls
        Loaded -= OnPageLoaded;

        // Wait a bit to ensure the page is fully rendered and visible
        await Task.Delay(100);

        // Hide Schedule page overlay after Home page is fully rendered and visible
        _viewModel?.HideSchedulePageOverlay();
    }

    private void OnScheduleItemTapped(object? sender, TappedEventArgs e)
    {
        View? container = sender as View;
        if (container == null || container.BindingContext is not ScheduleListItem item)
        {
            return;
        }

        // Get the tap position relative to the container
        var tapPosition = e.GetPosition(container);
        if (!tapPosition.HasValue)
        {
            // If we can't get position, navigate (fallback behavior)
            _viewModel.ViewScheduleCommand.Execute(item);
            return;
        }

        // Check if the tap position is within any Button or Switch bounds
        if (IsTapOnInteractiveControl(container, tapPosition.Value))
        {
            // Tap was on a button or switch - don't navigate
            return;
        }

        // Tap was not on a button or switch - navigate to schedule view
        _viewModel.ViewScheduleCommand.Execute(item);
    }

    private bool IsTapOnInteractiveControl(View view, Point tapPosition)
    {
        // Check if this view itself is a Button, SfButton, or Switch and contains the tap
        // Bounds are relative to the parent, and tapPosition is in the same coordinate space
        if ((view is Button || view is SfButton || view is Switch))
        {
            var bounds = view.Bounds;
            if (bounds.Contains(tapPosition))
            {
                return true;
            }
        }

        // Recursively check children
        if (view is Layout layout)
        {
            foreach (var child in layout.Children)
            {
                if (child is View childView)
                {
                    var childBounds = childView.Bounds;
                    // Check if tap is within this child's bounds (bounds are relative to current parent)
                    if (childBounds.Contains(tapPosition))
                    {
                        // Convert tap position to child's coordinate space for recursive check
                        var relativePoint = new Point(
                            tapPosition.X - childBounds.X,
                            tapPosition.Y - childBounds.Y);

                        // Recursively check this child
                        if (IsTapOnInteractiveControl(childView, relativePoint))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Reset schedule state when navigating back to home
        // This ensures only one schedule is in state at any time
        _viewModel?.ResetScheduleState();
        // Reset flag when page appears again (e.g., navigating back to it)
        _hasHandledFirstLoad = false;
        Loaded += OnPageLoaded;
    }

    protected override bool OnBackButtonPressed()
    {
        // Prevent back navigation on Home page - it's the root page
        return true;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            // ViewModel was injected via constructor, so dispose it
            if (_viewModel is IDisposable disposable)
            {
                disposable.Dispose();
            }
            // Clear BindingContext to break reference and allow garbage collection
            BindingContext = null;
            _isDisposed = true;
        }
    }
}