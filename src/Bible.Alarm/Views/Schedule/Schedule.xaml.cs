#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels;
using Serilog;
using Microsoft.Maui.Controls.Xaml;

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
            Log.Debug("Schedule.xaml.cs: Constructor - Set busy overlay to visible");
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
        // Only handle once per page instance
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

        Log.Debug("Schedule.xaml.cs: Content loaded, calling OnContentLoaded. isContentLoaded={IsContentLoaded}", isContentLoaded);

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
                Log.Debug("Schedule.xaml.cs: ViewModel not ready yet, keeping overlay visible (default)");
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
                    Log.Debug("Schedule.xaml.cs: Syncing overlay with state - showing (IsVisible=true)");
                    scheduleBusyOverlay.IsVisible = true;
                }
            }
            else if (scheduleBusyOverlay.IsVisible && isContentLoaded)
            {
                // ViewModel says hide AND content is loaded - safe to hide
                Log.Debug("Schedule.xaml.cs: Syncing overlay with state - hiding (IsVisible=false, content loaded)");
                scheduleBusyOverlay.IsVisible = false;
            }
            else if (!shouldBeVisible && !isContentLoaded)
            {
                // ViewModel says hide but content not loaded yet - keep visible
                Log.Debug("Schedule.xaml.cs: ViewModel says hide but content not loaded - keeping overlay visible");
            }
        });
    }

    private async Task LoadScheduleContentAsync()
    {
#if DEBUG
        var contentLoadStartTime = DateTime.UtcNow;
        Log.Information("[PERF] Schedule page: Starting content load at {StartTime}", contentLoadStartTime);
#endif

        // Load content on UI thread (XAML parsing must be on UI thread)
        await this.Dispatcher.DispatchAsync(async () =>
        {
            // Create the content view with all heavy XAML
            var scheduleContent = new ScheduleContent
            {
                BindingContext = viewModel
            };

            // Add content to container
            contentContainer.Content = scheduleContent;

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
                        Log.Debug("Schedule.xaml.cs: Preventing overlay hide - content not loaded yet");
                        return;
                    }

                    Log.Debug("Schedule.xaml.cs: State changed - overlay IsVisible={Value}", newValue);
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

        // If content is already loaded, we're coming back from a modal - don't show overlay
        if (isContentLoaded)
        {
            Log.Debug("Schedule.xaml.cs: OnAppearing - Content already loaded, skipping overlay (modal was closed)");
            return;
        }

        // Reset flags when page appears again (e.g., navigating back to it)
        hasHandledFirstLoad = false;
        isContentLoaded = false;

        // Reset ViewModel's content loaded flag as well
        viewModel?.ResetContentLoaded();

        // Force overlay visible immediately when page appears (before content loads)
        // This ensures the busy indicator is visible even if ViewModel state hasn't synced yet
        if (scheduleBusyOverlay != null)
        {
            scheduleBusyOverlay.IsVisible = true;
            Log.Debug("Schedule.xaml.cs: OnAppearing - Forcing overlay visible (will sync with state later)");
        }

        // Sync with state (but won't hide if content not loaded due to our fix in SyncOverlayWithState)
        SyncOverlayWithState();

        // Load content asynchronously after page appears
        OnPageAppearing();
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
