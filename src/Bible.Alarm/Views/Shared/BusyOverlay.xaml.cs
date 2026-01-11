#nullable enable
using Serilog;

namespace Bible.Alarm.Views.Shared;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class BusyOverlay : ContentView
{
    private static readonly ILogger logger = Log.ForContext<BusyOverlay>();

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
                logger.Debug("BusyOverlay.IsVisible: Setting from {OldValue} to {NewValue} (via property setter)", oldValue, value);
                SetValue(IsVisibleProperty, value);
                // Note: OnIsVisibleChanged will be called automatically by BindableProperty, which handles opacity/InputTransparent
                // We only need to handle spinner start/stop here for immediate feedback

                // Force spinner to start/stop immediately when visibility changes
                // This ensures smooth animation without binding delays
                if (value)
                {
                    // Wait for next UI cycle to ensure XAML is fully loaded
                    this.Dispatcher.Dispatch(() =>
                    {
                        StartSpinnerImmediately();
                    });
                }
                else
                {
                    StopSpinnerImmediately();
                }
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
            var newBoolValue = (bool)newValue;
            var opacity = newBoolValue ? 1.0 : 0.0;
            var inputTransparent = !newBoolValue; // When visible, don't allow input through (false), when hidden, allow input through (true)
            logger.Debug("BusyOverlay.OnIsVisibleChanged: Property changed from {OldValue} to {NewValue} (via binding, opacity will be {Opacity}, inputTransparent will be {InputTransparent})",
                oldValue, newBoolValue, opacity, inputTransparent);

            // Set opacity and InputTransparent directly on both the ContentView itself and the overlay grid
            // This bypasses any binding delays and ensures the overlay behaves correctly as soon as IsVisible is set
            overlay.Dispatcher.Dispatch(() =>
            {
                // Set InputTransparent on the ContentView itself (this is critical - parent must allow input through)
                overlay.InputTransparent = inputTransparent;

                if (overlay.overlayGrid != null)
                {
                    overlay.overlayGrid.Opacity = opacity;
                    overlay.overlayGrid.InputTransparent = inputTransparent;
                    logger.Debug("BusyOverlay.OnIsVisibleChanged: Set ContentView.InputTransparent to {InputTransparent}, overlayGrid.Opacity to {Opacity}, overlayGrid.InputTransparent to {InputTransparent}", inputTransparent, opacity, inputTransparent);
                }
            });
        }
    }

    /// <summary>
    /// Forces the spinner to start immediately, bypassing binding delays.
    /// This ensures smooth animation from the moment the overlay becomes visible.
    /// </summary>
    public void StartSpinnerImmediately()
    {
        // Access the SfBusyIndicator directly and set IsRunning immediately
        // This bypasses the binding evaluation delay
        if (busyIndicator != null)
        {
            logger.Debug("BusyOverlay: Starting spinner immediately");
            busyIndicator.IsRunning = true;
            // Force layout update to ensure spinner is positioned correctly
            InvalidateMeasure();
        }
    }

    /// <summary>
    /// Forces the spinner to stop immediately.
    /// </summary>
    public void StopSpinnerImmediately()
    {
        if (busyIndicator != null)
        {
            logger.Debug("BusyOverlay: Stopping spinner immediately");
            busyIndicator.IsRunning = false;
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
        // OnIsVisibleChanged already handles opacity and InputTransparent setup
        // We only need to ensure spinner starts if overlay is visible when loaded
        if (IsVisible && busyIndicator != null)
        {
            logger.Debug("BusyOverlay: Loaded and visible, starting spinner immediately");
            busyIndicator.IsRunning = true;
        }

        // Unsubscribe after first load
        Loaded -= OnBusyOverlayLoaded;
    }
}

