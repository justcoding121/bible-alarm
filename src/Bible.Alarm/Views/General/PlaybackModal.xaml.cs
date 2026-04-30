#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;

#if ANDROID
using AndroidX.Core.View;
using Android.Views;
using View = Android.Views.View;
#endif

namespace Bible.Alarm.Views.General;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class PlaybackModal : BaseContentPage, IDisposable
{
    protected override bool ApplyAndroidSafeAreaPadding => false;

    private volatile bool isDisposed;
    private bool hasHandledFirstLoad;
    private readonly PlaybackViewModel viewModel;
#if IOS
    private TapGestureRecognizer? portraitTapRecognizer;
    private TapGestureRecognizer? landscapeTapRecognizer;
#endif
#if ANDROID
    private bool _statusBarOffsetApplied;
#endif

    public PlaybackViewModel? ViewModel => BindingContext as PlaybackViewModel;

    public PlaybackModal(PlaybackViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        this.viewModel = viewModel;

        Loaded += OnPageLoaded;
        SizeChanged += OnSizeChanged;
    }

#if ANDROID
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (!isDisposed && Handler?.PlatformView != null)
        {
            ApplyStatusBarOffset();
        }
    }

    /// <summary>
    /// Ensures the page content is not hidden behind the status bar. On Android API 35+
    /// (edge-to-edge), MAUI's NavigationPage may or may not apply the status bar inset
    /// to pushed pages. This method checks the platform view's actual window position
    /// and only adds manual margins when the content is genuinely behind the status bar.
    /// </summary>
    private void ApplyStatusBarOffset()
    {
        if (_statusBarOffsetApplied || isDisposed)
        {
            return;
        }

        try
        {
            var activity = Platform.CurrentActivity;
            if (activity == null)
            {
                return;
            }

            var decorView = activity.Window?.DecorView;
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

            float density = activity.Resources?.DisplayMetrics?.Density ?? 1f;

            var platformView = Handler?.PlatformView as View;
            if (platformView != null)
            {
                var location = new int[2];
                platformView.GetLocationInWindow(location);
                int viewTopPx = location[1];

                if (viewTopPx >= statusBarHeightPx)
                {
                    _statusBarOffsetApplied = true;
                    return;
                }
            }

            double statusBarDip = statusBarHeightPx / (double)density;

            _statusBarOffsetApplied = true;

            PortraitMinimizeContainer.Margin = new Thickness(0, 4 + statusBarDip, 12, 0);
            MainContentArea.Margin = new Thickness(0, statusBarDip, 0, 0);

            if (LandscapeContent?.MinimizeContainer != null)
            {
                LandscapeContent.MinimizeContainer.Margin = new Thickness(0, 4 + statusBarDip, 8, 0);
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex, "Failed to apply status bar offset in PlaybackModal");
        }
    }
#endif

    private bool wasLandscape;

    private void OnSizeChanged(object? sender, EventArgs e)
    {
        if (isDisposed)
        {
            return;
        }

        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        var isLandscape = Width > Height;
        ViewModel?.SetIsLandscape(isLandscape);

        if (wasLandscape && !isLandscape)
        {
            RefreshPortraitArtworkSource();
        }

        wasLandscape = isLandscape;
    }

    /// <summary>
    /// The portrait container toggles IsVisible on orientation change, which causes MAUI
    /// to detach/reattach the Image handler (losing rendered content). A single UpdateValue
    /// call after the switch re-processes the source.
    /// </summary>
    private void RefreshPortraitArtworkSource()
    {
        Task.Delay(150).ContinueWith(_ =>
        {
            if (isDisposed)
            {
                return;
            }

            try
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        if (!isDisposed)
                        {
                            PortraitArtworkImage?.Handler?.UpdateValue(nameof(Image.Source));
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // Handler/control may be disposed on background thread; artwork refresh is best-effort.
                    }
                    catch (InvalidOperationException)
                    {
                        // Same: skip if MAUI rejects UpdateValue during teardown.
                    }
                });
            }
            catch (ObjectDisposedException)
            {
                // Delay callback may race with page disposal during artwork refresh.
            }
        });
    }

    private void OnPlaybackModalTapped(object? sender, TappedEventArgs e)
    {
        // In landscape, tapping anywhere should bring back the overlay controls.
        ViewModel?.NotifyLandscapeInteraction();
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        if (hasHandledFirstLoad)
        {
            return;
        }

        hasHandledFirstLoad = true;
        Loaded -= OnPageLoaded;

        if (isDisposed)
        {
            return;
        }

        if (Width <= Height)
        {
            RefreshPortraitArtworkSource();
        }

        WireLandscapeContentEvents();

#if ANDROID
        if (!isDisposed)
        {
            ApplyStatusBarOffset();
        }
#endif

#if IOS
        SetupIOSTapToSeek();
        ForceIOSLayoutMeasurement();
#endif

        await Task.Delay(1000);

        if (!isDisposed)
        {
            WeakReferenceMessenger.Default.Send(new PlaybackModalOpenedMessage());
        }
    }

    private void WireLandscapeContentEvents()
    {
        if (LandscapeContent == null)
        {
            return;
        }

        if (LandscapeContent.StopButton != null)
        {
            LandscapeContent.StopButton.Clicked += OnStopButtonClicked;
        }

        var slider = LandscapeContent.ProgressSlider;
        if (slider != null)
        {
            slider.ValueChanged += OnSliderValueChanged;
            slider.DragStarted += OnSliderDragStarted;
            slider.DragCompleted += OnSliderDragCompleted;
        }
    }

    private void UnwireLandscapeContentEvents()
    {
        if (LandscapeContent == null)
        {
            return;
        }

        if (LandscapeContent.StopButton != null)
        {
            LandscapeContent.StopButton.Clicked -= OnStopButtonClicked;
        }

        var slider = LandscapeContent.ProgressSlider;
        if (slider != null)
        {
            slider.ValueChanged -= OnSliderValueChanged;
            slider.DragStarted -= OnSliderDragStarted;
            slider.DragCompleted -= OnSliderDragCompleted;
        }
    }

#if IOS
    private void SetupIOSTapToSeek()
    {
        RemoveIOSTapRecognizers();
        if (ProgressSlider != null)
        {
            portraitTapRecognizer = new TapGestureRecognizer();
            portraitTapRecognizer.Tapped += OnSliderTapped;
            ProgressSlider.GestureRecognizers.Add(portraitTapRecognizer);
        }

        var landscapeSlider = LandscapeContent?.ProgressSlider;
        if (landscapeSlider != null)
        {
            landscapeTapRecognizer = new TapGestureRecognizer();
            landscapeTapRecognizer.Tapped += OnSliderTapped;
            landscapeSlider.GestureRecognizers.Add(landscapeTapRecognizer);
        }
    }

    private void RemoveIOSTapRecognizers()
    {
        if (portraitTapRecognizer != null)
        {
            portraitTapRecognizer.Tapped -= OnSliderTapped;
            ProgressSlider?.GestureRecognizers.Remove(portraitTapRecognizer);
            portraitTapRecognizer = null;
        }

        if (landscapeTapRecognizer != null)
        {
            landscapeTapRecognizer.Tapped -= OnSliderTapped;
            LandscapeContent?.ProgressSlider?.GestureRecognizers.Remove(landscapeTapRecognizer);
            landscapeTapRecognizer = null;
        }
    }

    private void ForceIOSLayoutMeasurement()
    {
        if (isDisposed)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                if (isDisposed)
                {
                    return;
                }

                // Force layout measurement by invalidating the metadata grid and parent containers
                // This ensures iOS properly measures the Auto-height row containing metadata labels
                if (MetadataGrid != null)
                {
                    MetadataGrid.InvalidateMeasure();
                }

                if (MainContentArea != null)
                {
                    MainContentArea.InvalidateMeasure();
                }

                // Force a second layout update after a short delay to ensure it happens after render
                Task.Delay(100).ContinueWith(_ =>
                {
                    if (isDisposed)
                    {
                        return;
                    }

                    try
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            try
                            {
                                if (isDisposed)
                                {
                                    return;
                                }

                                if (MetadataGrid != null)
                                {
                                    MetadataGrid.InvalidateMeasure();
                                }

                                if (MainContentArea != null)
                                {
                                    MainContentArea.InvalidateMeasure();
                                }
                            }
                            catch (ObjectDisposedException ex)
                            {
                                Log.Logger.Debug(ex, "PlaybackModal: Modal disposed during ForceIOSLayoutMeasurement");
                            }
                            catch (InvalidOperationException ex)
                            {
                                Log.Logger.Debug(ex, "PlaybackModal: Dispatcher/view no longer available during ForceIOSLayoutMeasurement");
                            }
                            catch (Exception ex)
                            {
                                Log.Logger.Debug(ex, "PlaybackModal: Native bridge or transitional-state exception during ForceIOSLayoutMeasurement");
                            }
                        });
                    }
                    catch (ObjectDisposedException ex)
                    {
                        Log.Logger.Debug(ex, "PlaybackModal: Modal disposed during delayed ForceIOSLayoutMeasurement");
                    }
                    catch (InvalidOperationException ex)
                    {
                        Log.Logger.Debug(ex, "PlaybackModal: Dispatcher no longer available during delayed ForceIOSLayoutMeasurement");
                    }
                });
            }
            catch (ObjectDisposedException ex)
            {
                Log.Logger.Debug(ex, "PlaybackModal: Modal disposed before ForceIOSLayoutMeasurement callback ran");
            }
            catch (InvalidOperationException ex)
            {
                Log.Logger.Debug(ex, "PlaybackModal: View hierarchy in transitional state during ForceIOSLayoutMeasurement");
            }
        });
    }
#endif

    protected override void OnAppearing()
    {
        base.OnAppearing();
        hasHandledFirstLoad = false;
        Loaded += OnPageLoaded;
    }

    protected override bool OnBackButtonPressed()
    {
        if (!isDisposed && ViewModel?.MinimizeCommand != null)
        {
            ViewModel.MinimizeCommand.Execute(null);
        }
        return true;
    }

    private async void OnStopButtonClicked(object? sender, EventArgs e)
    {
        // Fallback handler to ensure stop button always works
        // This ensures the command executes even if binding fails or command is blocked
        var logger = Log.Logger;
        logger.Information("Stop button clicked - OnStopButtonClicked handler fired");

        try
        {
            // Immediately switch Stop icon -> spinner and disable controls.
            ViewModel?.BeginStoppingUi();

            // Try command first
            if (ViewModel?.DismissCommand != null && ViewModel.DismissCommand.CanExecute(null))
            {
                logger.Debug("Executing DismissCommand via click handler");
                ViewModel.DismissCommand.Execute(null);
            }
            else
            {
                logger.Warning("DismissCommand is null or cannot execute, attempting direct stop");
                // If command fails, try direct stop
                var playbackService = ServiceProviderManager.GetService<IPlaybackService>();
                if (playbackService != null)
                {
                    // Allow the spinner to render before stopping playback.
                    await Task.Delay(50);
                    await playbackService.StopAsync();
                }
                else
                {
                    logger.Error("PlaybackService not available");
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error in OnStopButtonClicked, attempting direct stop");
            // If command execution fails, try direct stop
            try
            {
                ViewModel?.BeginStoppingUi();
                var playbackService = ServiceProviderManager.GetService<IPlaybackService>();
                if (playbackService != null)
                {
                    await playbackService.StopAsync();
                }
            }
            catch (Exception stopEx)
            {
                logger.Error(stopEx, "Direct stop also failed");
            }
        }
    }

    private bool isDragging;
#if IOS
    private bool isHandlingTap;
#endif
    private System.Timers.Timer? seekDebounceTimer;
    private double? pendingSeekValue;
    // Wait 200ms after last value change before seeking
    private const int SeekDebounceDelayMs = 200;

    private void OnSliderValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (isDisposed || ViewModel == null)
        {
            return;
        }

#if IOS
        // If we're handling a tap gesture, ignore ValueChanged to avoid double-handling
        // The tap gesture will handle the seek directly
        if (isHandlingTap)
        {
            return;
        }
#endif

        // On iOS, DragStarted/DragCompleted events might not fire reliably
        // So we need to handle both tap and drag through ValueChanged
        // Use debouncing: wait for user to stop interacting before seeking

        // Check if this is a programmatic update (from binding)
        // If IsUserInteracting is false, this is likely a programmatic update
        // However, on iOS, the first touch might not set IsUserInteracting yet
        // So we need to detect user interaction by checking if the value changed significantly
        // or if we're already in a drag state

        var isLikelyUserInteraction = ViewModel.IsUserInteracting || isDragging;

        // If not a user interaction, ignore (this is a programmatic update from binding)
        if (!isLikelyUserInteraction)
        {
            return;
        }

        // Stop any existing timer
        seekDebounceTimer?.Stop();
        seekDebounceTimer?.Dispose();

        // Store the value we want to seek to
        pendingSeekValue = e.NewValue;

        // If user is not yet marked as interacting, mark them now
        // (iOS might not fire DragStarted, so we detect it from ValueChanged)
        if (!ViewModel.IsUserInteracting)
        {
            // This is the first interaction - mark as dragging started
            isDragging = true;
            ViewModel.OnSliderDragStarted();
        }

        // Update visual position immediately (bypass Progress setter to avoid feedback loop)
        // This gives immediate visual feedback while dragging
        ViewModel.SetProgressDirectly(e.NewValue);

        // Set up debounce timer - seek when user stops interacting
        seekDebounceTimer = new System.Timers.Timer(SeekDebounceDelayMs);
        seekDebounceTimer.Elapsed += (s, args) =>
        {
            // Capture the timer that fired - it may differ from seekDebounceTimer if a new timer was created
            var timer = s as System.Timers.Timer;
            timer?.Stop();
            timer?.Dispose();
            
            // Only clear the field reference if it still points to this timer
            if (ReferenceEquals(seekDebounceTimer, timer))
            {
                seekDebounceTimer = null;
            }

            // User has stopped interacting - perform seek
            // Check if disposed before dispatching to main thread
            if (isDisposed)
            {
                return;
            }

            try
            {
                var pendingValue = pendingSeekValue;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        if (isDisposed || ViewModel == null || !pendingValue.HasValue)
                        {
                            return;
                        }

                        var value = pendingValue.Value;
                        pendingSeekValue = null;
                        isDragging = false;
                        ViewModel.OnSliderDragCompleted(value);
                    }
                    catch (ObjectDisposedException ex)
                    {
                        Log.Logger.Debug(ex, "PlaybackModal: Modal disposed during seek debounce callback");
                    }
                    catch (InvalidOperationException ex)
                    {
                        Log.Logger.Debug(ex, "PlaybackModal: View no longer available during seek debounce callback");
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Debug(ex, "PlaybackModal: Exception during seek debounce callback (e.g. CarPlay modal close)");
                    }
                });
            }
            catch (ObjectDisposedException ex)
            {
                Log.Logger.Debug(ex, "PlaybackModal: Modal disposed before seek debounce callback");
            }
            catch (InvalidOperationException ex)
            {
                Log.Logger.Debug(ex, "PlaybackModal: Dispatcher/main thread no longer available for seek debounce callback");
            }
        };
        seekDebounceTimer.AutoReset = false;
        seekDebounceTimer.Start();
    }

    private void OnSliderDragStarted(object? sender, EventArgs e)
    {
        if (isDisposed)
        {
            return;
        }

        // Stop any timer-based detection
        seekDebounceTimer?.Stop();
        seekDebounceTimer?.Dispose();
        seekDebounceTimer = null;

        isDragging = true;
        ViewModel?.OnSliderDragStarted();
    }

    private void OnSliderDragCompleted(object? sender, EventArgs e)
    {
        if (isDisposed)
        {
            return;
        }

        // Stop any timer-based detection
        seekDebounceTimer?.Stop();
        seekDebounceTimer?.Dispose();
        seekDebounceTimer = null;

        isDragging = false;
        if (sender is Slider slider && ViewModel != null)
        {
            // Cancel any pending debounced seek and perform immediate seek
            pendingSeekValue = null;
            ViewModel.OnSliderDragCompleted(slider.Value);
        }
    }

#if IOS
    private void OnSliderTapped(object? sender, TappedEventArgs e)
    {
        if (isDisposed || ViewModel == null || sender is not Slider slider)
        {
            return;
        }

        // Set flag to prevent ValueChanged from also handling this tap
        isHandlingTap = true;
        try
        {
            // On iOS, tapping the slider track doesn't automatically update the value
            // We need to calculate the progress based on tap position
            var tapPosition = e.GetPosition(slider);
            if (!tapPosition.HasValue)
            {
                return;
            }

            // Get slider width and calculate progress (0.0 to 1.0)
            var sliderWidth = slider.Width;
            if (sliderWidth <= 0)
            {
                // Slider width not available yet, try to get it from the bounds
                sliderWidth = slider.Bounds.Width;
                if (sliderWidth <= 0)
                {
                    return;
                }
            }

            var x = tapPosition.Value.X;
            var progress = Math.Max(0.0, Math.Min(1.0, x / sliderWidth));

            // Use the ViewModel's tap handler to perform the seek
            ViewModel.OnSliderTapped(progress);
        }
        finally
        {
            // Reset flag after a short delay to allow ValueChanged to process normally for drags
            Task.Delay(100).ContinueWith(_ =>
            {
                if (isDisposed)
                {
                    return;
                }

                try
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        try
                        {
                            if (!isDisposed)
                            {
                                isHandlingTap = false;
                            }
                        }
                        catch (ObjectDisposedException ex)
                        {
                            Log.Logger.Debug(ex, "PlaybackModal: Modal disposed during OnSliderTapped isHandlingTap reset");
                        }
                        catch (InvalidOperationException ex)
                        {
                            Log.Logger.Debug(ex, "PlaybackModal: View no longer available during OnSliderTapped isHandlingTap reset");
                        }
                        catch (Exception ex)
                        {
                            Log.Logger.Debug(ex, "PlaybackModal: Exception during OnSliderTapped isHandlingTap reset (transitional state)");
                        }
                    });
                }
                catch (ObjectDisposedException ex)
                {
                    Log.Logger.Debug(ex, "PlaybackModal: Modal disposed before OnSliderTapped isHandlingTap reset");
                }
                catch (InvalidOperationException ex)
                {
                    Log.Logger.Debug(ex, "PlaybackModal: Main thread invocation no longer available for OnSliderTapped isHandlingTap reset");
                }
            });
        }
    }
#endif

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (isDisposed)
        {
            return;
        }

        if (disposing)
        {
            try
            {
                seekDebounceTimer?.Stop();
                seekDebounceTimer?.Dispose();
            }
            catch (Exception)
            {
                // Seek debounce timer dispose is best-effort during modal teardown.
            }
            finally
            {
                seekDebounceTimer = null;
            }

            try
            {
                Loaded -= OnPageLoaded;
                SizeChanged -= OnSizeChanged;
                UnwireLandscapeContentEvents();
#if IOS
                RemoveIOSTapRecognizers();
#endif
            }
            catch (Exception)
            {
                // Ignore - page may be in transitional state (e.g. CarPlay disconnect)
            }

            pendingSeekValue = null;

            try
            {
                if (viewModel is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch (Exception)
            {
                // Ignore errors during ViewModel disposal
            }

            BindingContext = null;
        }

        isDisposed = true;
    }
}
