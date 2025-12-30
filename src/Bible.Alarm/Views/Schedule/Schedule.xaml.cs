#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels;
using Serilog;

namespace Bible.Alarm.Views.Schedule;

public partial class Schedule : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly ScheduleViewModel viewModel;

    public ScheduleViewModel? ViewModel => BindingContext as ScheduleViewModel;

    private bool hasHandledFirstLoad;

    public Schedule(ScheduleViewModel viewModel)
    {
#if DEBUG
        var constructorStartTime = DateTime.UtcNow;
        Log.Information("[PERF] Schedule page: Constructor started at {StartTime}", constructorStartTime);

        var initComponentStartTime = DateTime.UtcNow;
#endif
        InitializeComponent();
#if DEBUG
        var initComponentElapsed = (DateTime.UtcNow - initComponentStartTime).TotalMilliseconds;
        Log.Information("[PERF] Schedule page: InitializeComponent took {ElapsedMs}ms", initComponentElapsed);
#endif

        BindingContext = viewModel;
        this.viewModel = viewModel;

        // Setup gesture recognizers after page is loaded to support hot reload
        Loaded += OnPageLoaded;
        Loaded += SetupGestureRecognizers;

#if DEBUG
        var constructorElapsed = (DateTime.UtcNow - constructorStartTime).TotalMilliseconds;
        Log.Information("[PERF] Schedule page: Constructor completed in {ElapsedMs}ms", constructorElapsed);
#endif
    }

    private void SetupGestureRecognizers(object? sender, EventArgs e)
    {
        // Gesture recognizers are now handled in the container views
        // Unsubscribe after setup
        Loaded -= SetupGestureRecognizers;
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
        // Loaded fires after the page is in the visual tree, but we still need a small delay
        // to ensure all UI elements are fully rendered
        await Task.Delay(100);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            // Hide Home page overlay after Schedule page is fully rendered and visible
            viewModel?.HideHomePageOverlay();
        });

        // Subscribe to ViewModel property changes to manually update BusyOverlay if binding doesn't work
        if (viewModel != null)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScheduleViewModel.IsSchedulePageOverlayVisible))
        {
            // Manually update BusyOverlay if binding isn't working
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (scheduleBusyOverlay != null && viewModel != null)
                {
                    var newValue = viewModel.IsSchedulePageOverlayVisible;
                    Log.Debug("Schedule.xaml.cs: Manually updating BusyOverlay.IsVisible to {Value}", newValue);
                    scheduleBusyOverlay.IsVisible = newValue;
                }
            });
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Reset flag when page appears again (e.g., navigating back to it)
        hasHandledFirstLoad = false;
        Loaded += OnPageLoaded;
    }


    protected override bool OnBackButtonPressed()
    {
        ViewModel?.CancelCommand.Execute(null);
        return true;
    }

    public void Dispose()
    {
        if (!isDisposed)
        {
            // Unsubscribe from property changes
            if (viewModel != null)
            {
                viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

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
