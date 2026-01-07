#nullable enable
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.ViewModels;
using Serilog;
using Syncfusion.Maui.Buttons;
using Microsoft.Maui.Controls.Xaml;

namespace Bible.Alarm.Views;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class Home : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private bool hasHandledFirstLoad;
    private readonly HomeViewModel viewModel;

    public Home(HomeViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(vm);

        InitializeComponent();
        BindingContext = vm;
        viewModel = vm;

        // Use Loaded event which fires after the page is in the visual tree
        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        // Only handle once per page instance
        if (hasHandledFirstLoad)
        {
            return;
        }

        hasHandledFirstLoad = true;

        // Unsubscribe to avoid multiple calls
        Loaded -= OnPageLoaded;

        // Wait a bit to ensure the page is fully rendered and visible
        await Task.Delay(100);

        // Hide Schedule page overlay after Home page is fully rendered and visible
        viewModel.HideSchedulePageOverlay();

        // Check and show alarm settings modal on first app launch
        await viewModel.CheckAndShowAlarmSettingsOnFirstLaunchAsync();

        // Update floating button visibility based on permissions
        viewModel.UpdateFloatingButtonVisibility();

#if DEBUG
        // Log that home page is fully loaded with data
        var totalBootstrapTime = BootstrapTimingHelper.GetElapsedMilliseconds();
        Log.Information("[BOOTSTRAP] ✅ Home page fully loaded with data - Total bootstrap time: {TotalMs}ms", totalBootstrapTime);
#endif
    }

    private void OnAddScheduleButtonClicked(object? sender, EventArgs e)
    {
        Log.Information("OnAddScheduleButtonClicked: Button clicked! IsBootstrapComplete={IsBootstrapComplete}, Command CanExecute={CanExecute}",
            viewModel?.IsBootstrapComplete ?? false,
            viewModel?.AddScheduleCommand?.CanExecute(null) ?? false);

        // Manually execute the command to test
        if (viewModel?.AddScheduleCommand != null && viewModel.AddScheduleCommand.CanExecute(null))
        {
            Log.Information("OnAddScheduleButtonClicked: Manually executing command");
            viewModel.AddScheduleCommand.Execute(null);
        }
        else
        {
            Log.Warning("OnAddScheduleButtonClicked: Command cannot execute. IsBootstrapComplete={IsBootstrapComplete}",
                viewModel?.IsBootstrapComplete ?? false);
        }
    }

    private void OnScheduleItemTapped(object? sender, TappedEventArgs e)
    {
        View? container = sender as View;
        if (container == null || container.BindingContext is not ScheduleListItemViewModel item)
        {
            return;
        }

        // Get the tap position relative to the container
        var tapPosition = e.GetPosition(container);
        if (!tapPosition.HasValue)
        {
            // If we can't get position, navigate (fallback behavior)
            viewModel.ViewScheduleCommand.Execute(item);
            return;
        }

        // Check if the tap position is within any Button or Switch bounds
        if (IsTapOnInteractiveControl(container, tapPosition.Value))
        {
            // Tap was on a button or switch - don't navigate
            return;
        }

        // Tap was not on a button or switch - navigate to schedule view
        viewModel.ViewScheduleCommand.Execute(item);
    }

    private bool IsTapOnInteractiveControl(View view, Point tapPosition)
    {
        if (IsViewInteractiveControl(view, tapPosition))
        {
            return true;
        }

        return IsTapOnChildControl(view, tapPosition);
    }

    private static bool IsViewInteractiveControl(View view, Point tapPosition)
    {
        if (view is Button or SfButton or Switch)
        {
            return view.Bounds.Contains(tapPosition);
        }
        return false;
    }

    private bool IsTapOnChildControl(View view, Point tapPosition)
    {
        if (view is not Layout layout)
        {
            return false;
        }

        foreach (var child in layout.Children)
        {
            if (child is View childView && IsTapOnChild(childView, tapPosition))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsTapOnChild(View childView, Point tapPosition)
    {
        var childBounds = childView.Bounds;
        if (!childBounds.Contains(tapPosition))
        {
            return false;
        }

        var relativePoint = new Point(
            tapPosition.X - childBounds.X,
            tapPosition.Y - childBounds.Y);

        return IsTapOnInteractiveControl(childView, relativePoint);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Reset schedule state when navigating back to home
        // This ensures only one schedule is in state at any time
        viewModel?.ResetScheduleState();
        // Reset flag when page appears again (e.g., navigating back to it)
        hasHandledFirstLoad = false;
        Loaded += OnPageLoaded;
        
        // Update floating button visibility based on permissions
        viewModel?.UpdateFloatingButtonVisibility();
    }

    protected override bool OnBackButtonPressed() =>
        // Prevent back navigation on Home page - it's the root page
        true;

    public void Dispose()
    {
        if (!isDisposed)
        {
            // ViewModel was injected via constructor, so dispose it
            if (viewModel is IDisposable disposable)
            {
                disposable.Dispose();
            }
            // Clear BindingContext to break reference and allow garbage collection
            BindingContext = null;
            isDisposed = true;
        }
    }
}
