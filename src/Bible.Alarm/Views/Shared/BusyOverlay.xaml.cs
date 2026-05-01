#nullable enable
using System.Windows.Input;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.Views.Shared;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class BusyOverlay : ContentView
{
    private static readonly ILogger logger = Log.ForContext<BusyOverlay>();

    /// <summary>
    /// Hard timeout in milliseconds. After this time, the overlay will auto-hide.
    /// Default is 10 seconds (10000ms). Set to 0 to disable auto-hide timeout.
    /// </summary>
    public static readonly BindableProperty HardTimeoutMsProperty = BindableProperty.Create(
        nameof(HardTimeoutMs),
        typeof(int),
        typeof(BusyOverlay),
        10000, // 10 seconds default
        BindingMode.OneWay);

    /// <summary>
    /// Hard timeout in milliseconds. After this time, the overlay will auto-hide.
    /// Default is 10 seconds. Set to 0 to disable auto-hide timeout.
    /// </summary>
    public int HardTimeoutMs
    {
        get => (int)GetValue(HardTimeoutMsProperty);
        set => SetValue(HardTimeoutMsProperty, value);
    }

    /// <summary>
    /// Whether to show the cancel button at the bottom of the overlay.
    /// </summary>
    public static readonly BindableProperty ShowCancelButtonProperty = BindableProperty.Create(
        nameof(ShowCancelButton),
        typeof(bool),
        typeof(BusyOverlay),
        false,
        BindingMode.OneWay);

    /// <summary>
    /// Whether to show the cancel button at the bottom of the overlay.
    /// </summary>
    public bool ShowCancelButton
    {
        get => (bool)GetValue(ShowCancelButtonProperty);
        set => SetValue(ShowCancelButtonProperty, value);
    }

    /// <summary>
    /// Command to execute when the cancel button is tapped.
    /// </summary>
    public static readonly BindableProperty CancelCommandProperty = BindableProperty.Create(
        nameof(CancelCommand),
        typeof(ICommand),
        typeof(BusyOverlay),
        null,
        BindingMode.OneWay);

    /// <summary>
    /// Command to execute when the cancel button is tapped.
    /// </summary>
    public ICommand? CancelCommand
    {
        get => (ICommand?)GetValue(CancelCommandProperty);
        set => SetValue(CancelCommandProperty, value);
    }

    /// <summary>
    /// When true, the cancel button shows a progress indicator instead of the X icon (e.g. while close is in progress).
    /// </summary>
    public static readonly BindableProperty IsCancelBusyProperty = BindableProperty.Create(
        nameof(IsCancelBusy),
        typeof(bool),
        typeof(BusyOverlay),
        false,
        BindingMode.OneWay);

    /// <summary>
    /// When true, the cancel button shows a progress indicator instead of the X icon.
    /// </summary>
    public bool IsCancelBusy
    {
        get => (bool)GetValue(IsCancelBusyProperty);
        set => SetValue(IsCancelBusyProperty, value);
    }

    /// <summary>
    /// Whether a fetch error occurred; shows retry button to the left of cancel.
    /// </summary>
    public static readonly BindableProperty HasFetchErrorProperty = BindableProperty.Create(
        nameof(HasFetchError),
        typeof(bool),
        typeof(BusyOverlay),
        false,
        BindingMode.OneWay,
        propertyChanged: OnHasFetchErrorChanged);

    /// <summary>
    /// Whether a fetch error occurred; shows retry button to the left of cancel.
    /// </summary>
    public bool HasFetchError
    {
        get => (bool)GetValue(HasFetchErrorProperty);
        set => SetValue(HasFetchErrorProperty, value);
    }

    /// <summary>
    /// True when overlay is visible and not in error state (spinner runs only during progress).
    /// </summary>
    public static readonly BindableProperty IsSpinnerRunningProperty = BindableProperty.Create(
        nameof(IsSpinnerRunning),
        typeof(bool),
        typeof(BusyOverlay),
        false,
        BindingMode.OneWay);

    /// <summary>
    /// True when overlay is visible and not in error state (spinner runs only during progress).
    /// </summary>
    public bool IsSpinnerRunning
    {
        get => (bool)GetValue(IsSpinnerRunningProperty);
        set => SetValue(IsSpinnerRunningProperty, value);
    }

    private static void OnHasFetchErrorChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is BusyOverlay overlay)
            overlay.UpdateIsSpinnerRunning();
    }

    private void UpdateIsSpinnerRunning()
    {
        IsSpinnerRunning = IsVisible && !HasFetchError;
    }

    /// <summary>
    /// Command to execute when the retry button is tapped.
    /// </summary>
    public static readonly BindableProperty RetryCommandProperty = BindableProperty.Create(
        nameof(RetryCommand),
        typeof(ICommand),
        typeof(BusyOverlay),
        null,
        BindingMode.OneWay);

    /// <summary>
    /// Command to execute when the retry button is tapped.
    /// </summary>
    public ICommand? RetryCommand
    {
        get => (ICommand?)GetValue(RetryCommandProperty);
        set => SetValue(RetryCommandProperty, value);
    }

    /// <summary>
    /// Whether to show the progress bar section (progress text + progress bar).
    /// </summary>
    public static readonly BindableProperty ShowProgressProperty = BindableProperty.Create(
        nameof(ShowProgress),
        typeof(bool),
        typeof(BusyOverlay),
        false,
        BindingMode.OneWay);

    /// <summary>
    /// Whether to show the progress bar section.
    /// </summary>
    public bool ShowProgress
    {
        get => (bool)GetValue(ShowProgressProperty);
        set => SetValue(ShowProgressProperty, value);
    }

    /// <summary>
    /// The progress value (0.0 to 1.0).
    /// </summary>
    public static readonly BindableProperty ProgressPercentProperty = BindableProperty.Create(
        nameof(ProgressPercent),
        typeof(double),
        typeof(BusyOverlay),
        0.0,
        BindingMode.OneWay);

    /// <summary>
    /// The progress value (0.0 to 1.0).
    /// </summary>
    public double ProgressPercent
    {
        get => (double)GetValue(ProgressPercentProperty);
        set => SetValue(ProgressPercentProperty, value);
    }

    /// <summary>
    /// The progress text (e.g., "50%").
    /// </summary>
    public static readonly BindableProperty ProgressTextProperty = BindableProperty.Create(
        nameof(ProgressText),
        typeof(string),
        typeof(BusyOverlay),
        string.Empty,
        BindingMode.OneWay);

    /// <summary>
    /// The progress text (e.g., "50%").
    /// </summary>
    public string ProgressText
    {
        get => (string)GetValue(ProgressTextProperty);
        set => SetValue(ProgressTextProperty, value);
    }

    private CancellationTokenSource? timeoutCancellation;
    private CancellationTokenSource? spinnerStopCancellation;
    private bool isProcessingVisibilityChange;

    public static new readonly BindableProperty IsVisibleProperty = BindableProperty.Create(
        nameof(IsVisible),
        typeof(bool),
        typeof(BusyOverlay),
        false,
        BindingMode.OneWay,
        propertyChanged: OnIsVisibleChanged);

    public new bool IsVisible
    {
        get => (bool)GetValue(IsVisibleProperty);
        set
        {
            var oldValue = (bool)GetValue(IsVisibleProperty);
            if (oldValue != value)
            {
                logger.Debug(AppConstants.Logging.BusyOverlayDiagnosticsLog.IsVisibleSetterFromTo, oldValue, value);
                SetValue(IsVisibleProperty, value);
                // Note: OnIsVisibleChanged will be called automatically by BindableProperty, which handles:
                // - opacity/InputTransparent
                // - spinner start/stop
                // - hard timeout start/cancel
            }
        }
    }

    /// <summary>
    /// Starts the hard timeout that will auto-hide the overlay after HardTimeoutMs.
    /// </summary>
    private void StartHardTimeout()
    {
        CancelHardTimeout();

        var timeoutMs = HardTimeoutMs;
        if (timeoutMs <= 0)
        {
            return; // Timeout disabled
        }

        timeoutCancellation = new CancellationTokenSource();
        var token = timeoutCancellation.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(timeoutMs, token);

                if (!token.IsCancellationRequested)
                {
                    logger.Warning(AppConstants.Logging.BusyOverlayDiagnosticsLog.HardTimeoutReachedAutoHiding, timeoutMs);
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        IsVisible = false;
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // Timeout cancelled (overlay was hidden normally)
            }
        }, token);
    }

    /// <summary>
    /// Cancels the hard timeout.
    /// </summary>
    private void CancelHardTimeout()
    {
        if (timeoutCancellation != null)
        {
            SafeTeardown.CancelDisposeNoThrow(timeoutCancellation);
            timeoutCancellation = null;
        }
    }

    /// <summary>
    /// Applies the hide state (opacity 0, input transparent, stop spinner). Used when the normal
    /// hide transition was skipped due to re-entrancy so the overlay does not stay visible forever.
    /// </summary>
    private static void ApplyDeferredHideVisualState(BusyOverlay overlay)
    {
        if (overlay.Window == null)
        {
            FinishDeferredHideTeardownWithoutWindow(overlay);
            return;
        }

        overlay.InputTransparent = true;
        if (overlay.overlayGrid != null)
        {
            overlay.overlayGrid.Opacity = 0;
            overlay.overlayGrid.InputTransparent = true;
        }

        FinishDeferredHideTeardownWithoutWindow(overlay);
        logger.Debug(AppConstants.Logging.BusyOverlayDiagnosticsLog.DeferredApplyHideApplied);
    }

    private static void DeferredApplyHide(BusyOverlay overlay)
    {
        void ApplyHide() => ApplyDeferredHideVisualState(overlay);

        try
        {
            overlay.Dispatcher.Dispatch(() =>
            {
                try
                {
                    ApplyHide();
                }
                catch (ObjectDisposedException ex)
                {
                    WarnRecoverDeferredHide(overlay, ex, AppConstants.Logging.BusyOverlayDiagnosticsLog.DeferredApplyHideObjectDisposedSkippingUpdate);
                }
                catch (InvalidOperationException ex)
                {
                    WarnRecoverDeferredHide(overlay, ex, AppConstants.Logging.BusyOverlayDiagnosticsLog.DeferredApplyHideInvalidOperationSkippingUpdate);
                }
                catch (Exception ex)
                {
                    RecoverDeferredHideAfterUnknownFailure(overlay, ex);
                }
            });
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.BusyOverlayDiagnosticsLog.DeferredApplyHideFailedDispatchUsingMainThread);
            try
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try { ApplyHide(); }
                    catch (Exception) { overlay.isProcessingVisibilityChange = false; }
                });
            }
            catch (Exception)
            {
                overlay.isProcessingVisibilityChange = false;
            }
        }
    }

    /// <summary>
    /// When a visibility toggle arrives while already processing one, optionally defer hide (caller returns true — stop processing).
    /// </summary>
    private static bool TryHandleBusyOverlayReentrantHide(BusyOverlay overlay, bool newBoolValue)
    {
        if (!overlay.isProcessingVisibilityChange)
        {
            return false;
        }

        if (newBoolValue)
        {
            logger.Debug(AppConstants.Logging.BusyOverlayDiagnosticsLog.OnIsVisibleChangedSkippingAlreadyProcessing);
            return true;
        }

        logger.Debug(AppConstants.Logging.BusyOverlayDiagnosticsLog.OnIsVisibleChangedDeferringHideAlreadyProcessing);
        DeferredApplyHide(overlay);
        return true;
    }

    /// <summary>
    /// Applies opacity, input routing, spinner, and hard-timeout state from an IsVisible change (dispatched onto UI thread).
    /// </summary>
    private static void ApplyBusyOverlayIsVisibleOpacityAndSpinner(BusyOverlay overlay, bool newBoolValue, double opacity, bool inputTransparent)
    {
        if (overlay.Window == null)
        {
            overlay.CancelHardTimeout();
            if (!newBoolValue)
            {
                overlay.StopSpinnerAfterDelay();
            }

            return;
        }

        overlay.InputTransparent = inputTransparent;

        if (overlay.overlayGrid != null)
        {
            overlay.overlayGrid.Opacity = opacity;
            overlay.overlayGrid.InputTransparent = inputTransparent;
            logger.Debug(AppConstants.Logging.BusyOverlayDiagnosticsLog.OnIsVisibleChangedSetContentAndGridInputTransparent,
                inputTransparent, opacity, inputTransparent);
        }

        if (newBoolValue)
        {
            overlay.UpdateIsSpinnerRunning();
            overlay.CancelSpinnerStop();
            if (overlay.IsSpinnerRunning)
            {
                overlay.StartSpinnerImmediately();
            }

            overlay.StartHardTimeout();
        }
        else
        {
            overlay.CancelHardTimeout();
            overlay.StopSpinnerAfterDelay();
        }
    }

    private static void FinishDeferredHideTeardownWithoutWindow(BusyOverlay overlay)
    {
        overlay.CancelHardTimeout();
        overlay.StopSpinnerAfterDelay();
        overlay.isProcessingVisibilityChange = false;
    }


    private static void WarnRecoverDeferredHide(BusyOverlay overlay, Exception ex, string diagnosticsLogConstant)
    {
        logger.Warning(ex, diagnosticsLogConstant);
        FinishDeferredHideTeardownWithoutWindow(overlay);
    }

    private static void RecoverDeferredHideAfterUnknownFailure(BusyOverlay overlay, Exception ex)
    {
        logger.Warning(ex, AppConstants.Logging.BusyOverlayDiagnosticsLog.DeferredApplyHideFailedToApplyHide);
        overlay.isProcessingVisibilityChange = false;
    }

    private static void LogWarnCancelTimeoutAndSpinnerIfHiding(BusyOverlay overlay, bool visible, Exception ex, string diagnosticsLogConstant)
    {
        logger.Warning(ex, diagnosticsLogConstant);
        overlay.CancelHardTimeout();
        if (!visible)
        {
            overlay.StopSpinnerAfterDelay();
        }
    }

    private static void RunIsVisibleChangedApplyDispatch(BusyOverlay overlay, bool newBoolValue, Action apply)
    {
        try
        {
            overlay.Dispatcher.Dispatch(() =>
            {
                try
                {
                    apply();
                }
                catch (ObjectDisposedException ex)
                {
                    LogWarnCancelTimeoutAndSpinnerIfHiding(overlay, newBoolValue, ex,
                        AppConstants.Logging.BusyOverlayDiagnosticsLog.OnIsVisibleChangedObjectDisposedDuringVisibilityApply);
                }
                catch (InvalidOperationException ex)
                {
                    LogWarnCancelTimeoutAndSpinnerIfHiding(overlay, newBoolValue, ex,
                        AppConstants.Logging.BusyOverlayDiagnosticsLog.OnIsVisibleChangedInvalidOperationDuringVisibilityApply);
                }
                finally
                {
                    overlay.isProcessingVisibilityChange = false;
                }
            });
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.BusyOverlayDiagnosticsLog.OnIsVisibleChangedFailedDispatchFallingBackMainThread);
            try
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        apply();
                    }
                    catch (ObjectDisposedException ex2)
                    {
                        LogWarnCancelTimeoutAndSpinnerIfHiding(overlay, newBoolValue, ex2,
                            AppConstants.Logging.BusyOverlayDiagnosticsLog.OnIsVisibleChangedObjectDisposedDuringVisibilityApplyMainThreadFallback);
                    }
                    catch (InvalidOperationException ex2)
                    {
                        LogWarnCancelTimeoutAndSpinnerIfHiding(overlay, newBoolValue, ex2,
                            AppConstants.Logging.BusyOverlayDiagnosticsLog.OnIsVisibleChangedInvalidOperationDuringVisibilityApplyMainThreadFallback);
                    }
                    finally
                    {
                        overlay.isProcessingVisibilityChange = false;
                    }
                });
            }
            catch (Exception ex2)
            {
                logger.Warning(ex2, AppConstants.Logging.BusyOverlayDiagnosticsLog.OnIsVisibleChangedFailedToApplyOnMainThread);
                overlay.isProcessingVisibilityChange = false;
            }
        }
    }

    private static void OnIsVisibleChanged(BindableObject bindable, object oldValue, object newValue)
    {
        // The XAML bindings using x:Reference should update automatically
        // This handler ensures the property change is processed
        // Note: We now use Opacity instead of IsVisible for instant show/hide
        if (bindable is BusyOverlay overlay && oldValue != newValue)
        {
            var oldBoolValue = oldValue as bool? ?? false;
            var newBoolValue = (bool)newValue;
            
            // Double-check: if the boolean values are actually the same, skip processing
            if (oldBoolValue == newBoolValue)
            {
                return;
            }

            // Guard against rapid toggling - if we're already processing a change, skip (unless hiding)
            // This prevents infinite loops when binding and explicit sets conflict.
            // When transitioning to false (hide), we must not skip: otherwise the overlay can stay visible
            // forever (e.g. track modal sets IsBusy=false before the "show" dispatch has run).
            if (TryHandleBusyOverlayReentrantHide(overlay, newBoolValue))
            {
                return;
            }

            overlay.isProcessingVisibilityChange = true;
            
            var opacity = newBoolValue ? 1.0 : 0.0;
            // When visible, don't allow input through (false), when hidden, allow input through (true)
            var inputTransparent = !newBoolValue;
            logger.Debug(AppConstants.Logging.BusyOverlayDiagnosticsLog.OnIsVisibleChangedPropertyChangedBindingOpacity,
                oldValue, newBoolValue, opacity, inputTransparent);

            void Apply() =>
                ApplyBusyOverlayIsVisibleOpacityAndSpinner(overlay, newBoolValue, opacity, inputTransparent);

            // Start spinner synchronously before dispatch to prevent card-without-spinner on iOS.
            // The XAML binding on overlayGrid.Opacity fires immediately (card visible),
            // but Apply() is dispatched asynchronously. Without this, the spinner may never start
            // if the dispatch is delayed.
            if (newBoolValue && !overlay.HasFetchError)
            {
                overlay.IsSpinnerRunning = true;
                if (overlay.busyIndicator != null)
                    overlay.busyIndicator.IsRunning = true;
            }

            RunIsVisibleChangedApplyDispatch(overlay, newBoolValue, Apply);
        }
    }

    /// <summary>
    /// Forces the spinner to start immediately, bypassing binding delays.
    /// This ensures smooth animation from the moment the overlay becomes visible.
    /// </summary>
    public void StartSpinnerImmediately()
    {
        // Access the ActivityIndicator directly and set IsRunning immediately
        // This bypasses the binding evaluation delay
        if (busyIndicator != null)
        {
            logger.Debug(AppConstants.Logging.BusyOverlayDiagnosticsLog.StartingSpinnerImmediately);
            busyIndicator.IsRunning = true;
        }
    }

    /// <summary>
    /// Forces the spinner to stop immediately.
    /// </summary>
    public void StopSpinnerImmediately()
    {
        // Cancel any pending delayed stop
        CancelSpinnerStop();
        
        if (busyIndicator != null)
        {
            logger.Debug(AppConstants.Logging.BusyOverlayDiagnosticsLog.StoppingSpinnerImmediately);
            busyIndicator.IsRunning = false;
        }
    }

    /// <summary>
    /// Stops the spinner after a short delay to allow the opacity fade to complete.
    /// This ensures the spinner fades out smoothly with the card instead of disappearing instantly.
    /// </summary>
    private void StopSpinnerAfterDelay()
    {
        // Cancel any existing delayed stop
        CancelSpinnerStop();
        
        // Delay of 300ms should be enough for the opacity fade to complete
        // This matches typical MAUI animation durations
        spinnerStopCancellation = new CancellationTokenSource();
        var token = spinnerStopCancellation.Token;
        
        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, token);
                
                if (!token.IsCancellationRequested)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        if (busyIndicator != null)
                        {
                            logger.Debug(AppConstants.Logging.BusyOverlayDiagnosticsLog.StoppingSpinnerAfterFadeDelay);
                            busyIndicator.IsRunning = false;
                        }
                        UpdateIsSpinnerRunning();
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // Stop was cancelled (overlay became visible again)
            }
        }, token);
    }

    /// <summary>
    /// Cancels any pending delayed spinner stop.
    /// </summary>
    private void CancelSpinnerStop()
    {
        if (spinnerStopCancellation != null)
        {
            SafeTeardown.CancelDisposeNoThrow(spinnerStopCancellation);
            spinnerStopCancellation = null;
        }
    }

    public BusyOverlay()
    {
        InitializeComponent();

        // Ensure spinner starts when overlay is loaded if it's already visible
        Loaded += OnBusyOverlayLoaded;
    }

    private void OnBusyOverlayLoaded(object? sender, EventArgs e)
    {
        UpdateIsSpinnerRunning();
        if (IsVisible)
        {
            if (IsSpinnerRunning && busyIndicator != null)
            {
                logger.Debug(AppConstants.Logging.BusyOverlayDiagnosticsLog.LoadedAndVisibleStartingSpinnerImmediately);
                busyIndicator.IsRunning = true;
            }
            StartHardTimeout();
        }

        Loaded -= OnBusyOverlayLoaded;
    }
}

