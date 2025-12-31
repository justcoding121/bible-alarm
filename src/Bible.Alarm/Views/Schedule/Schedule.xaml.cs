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
    private bool isOverlayForcedVisible;

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

        // Subscribe to ViewModel property changes BEFORE setting overlay visible
        // This ensures we can prevent overlay from being hidden prematurely
        if (viewModel != null)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        // Ensure overlay is visible when page appears
        // This is critical for both viewing and adding schedules
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            // Mark overlay as forced visible - this prevents ViewModel from hiding it
            isOverlayForcedVisible = true;
            
            // Force overlay to be visible when page appears
            // This ensures it shows even if ViewModel state hasn't propagated yet
            if (scheduleBusyOverlay != null)
            {
                Log.Debug("Schedule.xaml.cs: Ensuring overlay is visible on page appear");
                scheduleBusyOverlay.IsVisible = true;
            }
        });

        // Small delay to ensure page is rendered and XAML is fully loaded
        await Task.Delay(50);
        
        // Now start the spinner after page is rendered
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (scheduleBusyOverlay != null)
            {
                Log.Debug("Schedule.xaml.cs: Starting spinner after page render");
                // Force spinner to start immediately, bypassing binding delays
                // This ensures smooth animation from the moment the overlay appears
                scheduleBusyOverlay.StartSpinnerImmediately();
            }
        });

        // Hide Home page overlay after Schedule page is visible
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            viewModel?.HideHomePageOverlay();
        });

        // Add delay after showing overlay to ensure spinner is animating smoothly
        // This improves perceived performance - user sees spinner working before heavy content loads
        await Task.Delay(100);

        // Load heavy content asynchronously after spinner is animating smoothly
        await LoadScheduleContentAsync();
        
        // Mark content as loaded - now allow ViewModel to control overlay
        isContentLoaded = true;
        
        // Allow overlay to be controlled by ViewModel now that content is loaded
        isOverlayForcedVisible = false;
        
        // If ViewModel wants to hide overlay, allow it
        if (viewModel != null && !viewModel.IsSchedulePageOverlayVisible)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (scheduleBusyOverlay != null)
                {
                    Log.Debug("Schedule.xaml.cs: Content loaded, hiding overlay as ViewModel requested");
                    scheduleBusyOverlay.IsVisible = false;
                }
            });
        }
    }

    private async Task LoadScheduleContentAsync()
    {
#if DEBUG
        var contentLoadStartTime = DateTime.UtcNow;
        Log.Information("[PERF] Schedule page: Starting content load at {StartTime}", contentLoadStartTime);
#endif

        // Load content on UI thread (XAML parsing must be on UI thread)
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            // Create the content view with all heavy XAML
            var scheduleContent = new ScheduleContent
            {
                BindingContext = viewModel
            };

            // Add content to container
            contentContainer.Content = scheduleContent;

            // Fade in content smoothly
            contentContainer.FadeTo(1.0, 200);
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
            // Manually update BusyOverlay if binding isn't working
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (scheduleBusyOverlay != null && viewModel != null)
                {
                    var newValue = viewModel.IsSchedulePageOverlayVisible;
                    
                    // Don't allow ViewModel to hide overlay if we've forced it visible
                    // This ensures smooth spinning throughout the loading process
                    if (!newValue && isOverlayForcedVisible)
                    {
                        Log.Debug("Schedule.xaml.cs: Preventing overlay hide - overlay is forced visible during loading");
                        return;
                    }
                    
                    // Don't hide overlay until content is loaded
                    // This ensures overlay stays visible when viewing existing schedules that load quickly
                    if (!newValue && !isContentLoaded)
                    {
                        Log.Debug("Schedule.xaml.cs: Preventing overlay hide - content not loaded yet");
                        return;
                    }
                    
                    Log.Debug("Schedule.xaml.cs: Updating BusyOverlay.IsVisible to {Value}", newValue);
                    scheduleBusyOverlay.IsVisible = newValue;
                }
            });
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Reset flags when page appears again (e.g., navigating back to it)
        hasHandledFirstLoad = false;
        isContentLoaded = false;
        isOverlayForcedVisible = false;
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
