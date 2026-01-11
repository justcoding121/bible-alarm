#nullable enable
using Bible.Alarm.ViewModels;
using Serilog;

namespace Bible.Alarm.Views.Schedule;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class Schedule : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly ScheduleViewModel viewModel;

    public ScheduleViewModel? ViewModel => BindingContext as ScheduleViewModel;

    private bool hasHandledFirstLoad;
    private bool isContentLoaded;

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

        // Set busy overlay to visible by default
        if (scheduleBusyOverlay != null)
        {
            scheduleBusyOverlay.IsVisible = true;
        }

        BindingContext = viewModel;
        this.viewModel = viewModel;

        // Setup gesture recognizers after page is loaded to support hot reload
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

    private async void OnPageAppearing()
    {
        // Only handle once per OnAppearing call
        if (hasHandledFirstLoad)
        {
            return;
        }

        hasHandledFirstLoad = true;

        // Subscribe to ViewModel property changes for overlay sync
        if (viewModel != null)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        // Hide Home page overlay after Schedule page is visible
        await this.Dispatcher.DispatchAsync(() =>
        {
            viewModel?.HideHomePageOverlay();
        });

        // Load heavy content asynchronously
        await LoadScheduleContentAsync();

        // Mark content as loaded
        isContentLoaded = true;

        // Notify ViewModel that content is loaded - it will hide overlay if containers are ready
        viewModel?.OnContentLoaded();
    }

    private void SyncOverlayWithState()
    {
        this.Dispatcher.Dispatch(() =>
        {
            if (scheduleBusyOverlay == null)
            {
                return;
            }

            // If ViewModel is not initialized yet, keep overlay visible (default state)
            // It will be synced when ViewModel is ready
            if (viewModel == null)
            {
                scheduleBusyOverlay.IsVisible = true;
                return;
            }

            var shouldBeVisible = viewModel.IsSchedulePageOverlayVisible;

            // Only sync if ViewModel says it should be visible, or if overlay is currently visible and ViewModel says hide
            // This prevents hiding the overlay prematurely during initial sync
            if (shouldBeVisible)
            {
                // ViewModel says show - always show
                if (!scheduleBusyOverlay.IsVisible)
                {
                    scheduleBusyOverlay.IsVisible = true;
                }
            }
            else if (scheduleBusyOverlay.IsVisible && isContentLoaded)
            {
                // ViewModel says hide AND content is loaded - safe to hide
                scheduleBusyOverlay.IsVisible = false;
            }
        });
    }

    private async Task LoadScheduleContentAsync()
    {
#if DEBUG
        var contentLoadStartTime = DateTime.UtcNow;
        Log.Information("[PERF] Schedule page: Starting content load at {StartTime}", contentLoadStartTime);
#endif

        // Allow spinner animation to run before heavy XAML parsing
        await Task.Yield();

        // Create the content view - XAML parsing must be on UI thread
        // Break into smaller steps with yields for smoother spinner
        ScheduleContent? scheduleContent = null;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            scheduleContent = new ScheduleContent();
        });

        // Yield to allow spinner to animate
        await Task.Yield();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (scheduleContent != null)
            {
                scheduleContent.BindingContext = viewModel;
            }
        });

        // Yield again
        await Task.Yield();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (scheduleContent != null)
            {
                contentContainer.Content = scheduleContent;
            }
        });

        // Yield before fade
        await Task.Yield();

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            // Fade in content smoothly
            await contentContainer.FadeToAsync(1.0, 200);
        });

#if DEBUG
        var contentLoadElapsed = (DateTime.UtcNow - contentLoadStartTime).TotalMilliseconds;
        Log.Information("[PERF] Schedule page: Content load completed in {ElapsedMs}ms", contentLoadElapsed);
#endif
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScheduleViewModel.IsSchedulePageOverlayVisible))
        {
            // State is the source of truth - sync overlay with ViewModel
            // But only hide if content is loaded (prevents hiding before content renders)
            this.Dispatcher.Dispatch(() =>
            {
                if (scheduleBusyOverlay != null && viewModel != null)
                {
                    var newValue = viewModel.IsSchedulePageOverlayVisible;

                    // Don't hide overlay until content is loaded
                    if (!newValue && !isContentLoaded)
                    {
                        return;
                    }

                    scheduleBusyOverlay.IsVisible = newValue;
                }
            });
        }

        // Also check if we should hide overlay when containers become ready (after content is loaded)
        // This handles the case where containers signal ready AFTER content loads
        if (isContentLoaded && viewModel != null)
        {
            viewModel.OnContentLoaded();
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Check if returning from a modal - if content is already loaded, we're returning from a modal
        // In this case, skip full reinitialization to avoid showing the spinner unnecessarily
        if (contentContainer?.Content != null && isContentLoaded)
        {
            // Just returning from a modal - sync overlay state and return
            SyncOverlayWithState();
            return;
        }

        // Full initialization for new page navigation (not returning from modal)
        hasHandledFirstLoad = false;
        isContentLoaded = false;
        viewModel?.ResetContentLoaded();

        // Reinitialize containers (critical on physical devices where page may be cached)
        viewModel?.OnPageReappearing();

        // Force overlay visible immediately
        if (scheduleBusyOverlay != null)
        {
            scheduleBusyOverlay.IsVisible = true;
        }

        SyncOverlayWithState();
        OnPageAppearing();
    }


    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Disposal happens in Dispose() when page is popped from navigation stack
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
            if (viewModel != null)
            {
                viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            if (viewModel is IDisposable disposable)
            {
                disposable.Dispose();
            }

            BindingContext = null;
            isDisposed = true;
        }
    }
}
