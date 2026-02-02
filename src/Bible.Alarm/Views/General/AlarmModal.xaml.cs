#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.ViewModels.Shared;
using Serilog;

namespace Bible.Alarm.Views.General;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class AlarmModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private bool hasHandledFirstLoad;
    private readonly AlarmViewModel viewModel;

    public AlarmViewModel? ViewModel => BindingContext as AlarmViewModel;

    public AlarmModal(AlarmViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        this.viewModel = viewModel;

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

        // Wait a bit to ensure the modal is fully rendered and visible
        await Task.Delay(100);

#if IOS
        // On iOS, Slider doesn't support tap-to-seek natively, so add TapGestureRecognizer
        SetupIOSTapToSeek();
#endif

        // Hide Home page overlay after Alarm Modal is fully rendered and visible
        viewModel?.HideHomePageOverlay();
    }

#if IOS
    private void SetupIOSTapToSeek()
    {
        if (ProgressSlider != null)
        {
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += OnSliderTapped;
            ProgressSlider.GestureRecognizers.Add(tapGesture);
        }
    }
#endif

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Reset flag when modal appears again
        hasHandledFirstLoad = false;
        Loaded += OnPageLoaded;
    }

    protected override bool OnBackButtonPressed()
    {
        if (!isDisposed && ViewModel?.DismissCommand != null)
        {
            ViewModel.DismissCommand.Execute(null);
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
            // Immediately hide playback controls/progress while stop completes.
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
    private const int SeekDebounceDelayMs = 200; // Wait 200ms after last value change before seeking

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
            seekDebounceTimer.Stop();
            seekDebounceTimer.Dispose();
            seekDebounceTimer = null;

            // User has stopped interacting - perform seek
            // Check if disposed before accessing Dispatcher or ViewModel
            if (isDisposed)
            {
                return;
            }

            try
            {
                this.Dispatcher.Dispatch(() =>
                {
                    // Double-check disposed state and ViewModel availability
                    if (isDisposed || ViewModel == null || !pendingSeekValue.HasValue)
                    {
                        return;
                    }

                    var value = pendingSeekValue.Value;
                    pendingSeekValue = null;
                    isDragging = false;
                    ViewModel.OnSliderDragCompleted(value);
                });
            }
            catch (ObjectDisposedException)
            {
                // Modal was disposed, ignore
            }
            catch (InvalidOperationException)
            {
                // Dispatcher is no longer available, ignore
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
                // Check if disposed before accessing Dispatcher
                if (isDisposed)
                {
                    return;
                }

                try
                {
                    this.Dispatcher.Dispatch(() =>
                    {
                        if (!isDisposed)
                        {
                            isHandlingTap = false;
                        }
                    });
                }
                catch (ObjectDisposedException)
                {
                    // Modal was disposed, ignore
                }
                catch (InvalidOperationException)
                {
                    // Dispatcher is no longer available, ignore
                }
            });
        }
    }
#endif

    public void Dispose()
    {
        if (!isDisposed)
        {
            isDisposed = true;

            // Clean up timer first to prevent callbacks from accessing disposed objects
            try
            {
                seekDebounceTimer?.Stop();
                seekDebounceTimer?.Dispose();
            }
            catch
            {
                // Ignore errors during timer cleanup
            }
            finally
            {
                seekDebounceTimer = null;
            }

            // Clear pending seek value
            pendingSeekValue = null;

            // ViewModel was injected via constructor, so dispose it
            try
            {
                if (viewModel is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch
            {
                // Ignore errors during ViewModel disposal
            }

            // Clear BindingContext to break reference and allow garbage collection
            BindingContext = null;
        }
    }
}
