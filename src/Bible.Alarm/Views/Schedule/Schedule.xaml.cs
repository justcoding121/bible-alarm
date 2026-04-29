#nullable enable
using Bible.Alarm.ViewModels;
using Serilog;

namespace Bible.Alarm.Views.Schedule;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class Schedule : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private bool hasHandledFirstLoad;
    private bool isContentLoaded;

    // Assigned asynchronously after PushAsync by NavigationService.InitializeViewModelAsync.
    // Null until then — the BusyOverlay FallbackValue=True keeps the spinner visible in the gap.
    private ScheduleViewModel? viewModel;

    public ScheduleViewModel? ViewModel => BindingContext as ScheduleViewModel;

    public Schedule()
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

        var constructorElapsed = (DateTime.UtcNow - constructorStartTime).TotalMilliseconds;
        Log.Information("[PERF] Schedule page: Constructor completed in {ElapsedMs}ms", constructorElapsed);
#endif

        Loaded += SetupGestureRecognizers;
    }

    /// <summary>
    /// Called by NavigationService after PushAsync returns and the spinner is visible.
    /// Sets the ViewModel, subscribes to property changes, then loads the heavy XAML content.
    /// </summary>
    public async Task InitializeViewModelAsync(ScheduleViewModel vm)
    {
        if (isDisposed)
        {
            if (vm is IDisposable d)
                d.Dispose();
            return;
        }

        viewModel = vm;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            BindingContext = vm;
            // Guard against duplicate subscriptions if page is reused
            vm.PropertyChanged -= OnViewModelPropertyChanged;
            vm.PropertyChanged += OnViewModelPropertyChanged;
            hasHandledFirstLoad = true;
            vm.HideHomePageOverlay();
        });

        await LoadScheduleContentAsync();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (!isDisposed)
            {
                isContentLoaded = true;
                vm.OnContentLoaded();
            }
        });
    }

    private void SetupGestureRecognizers(object? sender, EventArgs e)
    {
        Loaded -= SetupGestureRecognizers;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Returning from a modal — content already loaded, just sync state.
        if (contentContainer?.Content != null && isContentLoaded)
        {
            SyncOverlayWithState();
#if WINDOWS
            EnsureContentVisibleAfterModalClosed();
#endif
            return;
        }

        // ViewModel not yet assigned — NavigationService will call InitializeViewModelAsync
        // once the ViewModel is resolved. Spinner is kept visible by FallbackValue=True.
        if (viewModel == null)
        {
            return;
        }

        // Page reappearing with an existing ViewModel (physical device page reuse).
        hasHandledFirstLoad = false;
        isContentLoaded = false;
        viewModel.ResetContentLoaded();
        viewModel.OnPageReappearing();
        SyncOverlayWithState();
        OnPageAppearing();
    }

    /// <summary>
    /// Handles content loading when a page instance is reused (physical device caching).
    /// For fresh navigations the equivalent work is done in InitializeViewModelAsync.
    /// </summary>
    private async void OnPageAppearing()
    {
        if (hasHandledFirstLoad)
        {
            return;
        }

        hasHandledFirstLoad = true;

        if (viewModel != null)
        {
            // Guard against duplicate subscriptions
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        await this.Dispatcher.DispatchAsync(() =>
        {
            viewModel?.HideHomePageOverlay();
        });

        await LoadScheduleContentAsync();

        isContentLoaded = true;
        viewModel?.OnContentLoaded();
    }

    private static void SyncOverlayWithState()
    {
        // Binding handles overlay visibility automatically.
    }

    /// <summary>
    /// Call after a modal is closed (e.g. section modal on fetch failure). WinUI can leave the underlying
    /// page with a blank or stale visual; force content visible, input enabled, and layout refresh.
    /// </summary>
    public void EnsureContentVisibleAfterModalClosed()
    {
        InputTransparent = false;
        if (Content != null)
            Content.InputTransparent = false;
        if (contentContainer != null)
        {
            contentContainer.Opacity = 1;
            contentContainer.InputTransparent = false;
        }

#if WINDOWS
        try
        {
            var nativeWindow = Application.Current?.Windows is { Count: > 0 } wins
                ? wins[0].Handler?.PlatformView as Microsoft.UI.Xaml.Window
                : null;
            if (nativeWindow?.Content is Microsoft.UI.Xaml.FrameworkElement rootElement)
            {
                rootElement.UpdateLayout();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Schedule: WinUI UpdateLayout failed (best-effort)");
        }
#else
        InvalidateMeasure();
#endif
    }

    private async Task LoadScheduleContentAsync()
    {
#if DEBUG
        var contentLoadStartTime = DateTime.UtcNow;
        Log.Information("[PERF] Schedule page: Starting content load at {StartTime}", contentLoadStartTime);
#endif

        // Give the spinner at least ~5 frames (80ms at 60fps) to visibly animate before
        // new ScheduleContent() blocks the UI thread for its XAML parse. A bare Task.Yield()
        // only cedes one scheduler tick which is not enough for the native ActivityIndicator
        // animation to render, making the spinner appear frozen to the user.
        await Task.Delay(80);

        ScheduleContent? scheduleContent = null;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            scheduleContent = new ScheduleContent();
        });

        await Task.Yield();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (scheduleContent != null)
            {
                scheduleContent.BindingContext = viewModel;
            }
        });

        await Task.Yield();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (scheduleContent != null)
            {
                contentContainer.Content = scheduleContent;
            }
        });

        await Task.Yield();

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await contentContainer.FadeToAsync(1.0, 200);
        });

#if DEBUG
        var contentLoadElapsed = (DateTime.UtcNow - contentLoadStartTime).TotalMilliseconds;
        Log.Information("[PERF] Schedule page: Content load completed in {ElapsedMs}ms", contentLoadElapsed);
#endif
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var isContainerAssignment = e.PropertyName is
            nameof(ScheduleViewModel.BibleSelectionContainerViewModel) or
            nameof(ScheduleViewModel.MusicSelectionContainerViewModel) or
            nameof(ScheduleViewModel.NumberOfTrackContainerViewModel) or
            nameof(ScheduleViewModel.ScheduleDetailsContainerViewModel);

        if (isContainerAssignment && isContentLoaded && viewModel != null)
        {
            viewModel.OnContentLoaded();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        ViewModel?.StopPermissionCheckTasks();
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
