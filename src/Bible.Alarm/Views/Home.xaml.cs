using Bible.Alarm.ViewModels;
using Microsoft.Maui.Controls;

namespace Bible.Alarm.Views;

public partial class Home : ContentPage, IDisposable
{
    private bool _isDisposed;
    private readonly HomeViewModel _viewModel;

    public Home(HomeViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _viewModel = vm;
    }

    private void OnScheduleItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Grid grid || grid.BindingContext is not ScheduleListItem item)
        {
            return;
        }

        // Get the tap position relative to the Grid
        var tapPosition = e.GetPosition(grid);
        if (!tapPosition.HasValue)
        {
            // If we can't get position, navigate (fallback behavior)
            _viewModel.ViewScheduleCommand.Execute(item);
            return;
        }

        // Check if the tap position is within any Button or Switch bounds
        if (IsTapOnInteractiveControl(grid, tapPosition.Value))
        {
            // Tap was on a button or switch - don't navigate
            return;
        }

        // Tap was not on a button or switch - navigate to schedule view
        _viewModel.ViewScheduleCommand.Execute(item);
    }

    private bool IsTapOnInteractiveControl(View view, Point tapPosition)
    {
        // Check if this view itself is a Button or Switch and contains the tap
        // Bounds are relative to the parent, and tapPosition is in the same coordinate space
        if ((view is Button || view is Switch))
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
            _isDisposed = true;
        }
    }
}