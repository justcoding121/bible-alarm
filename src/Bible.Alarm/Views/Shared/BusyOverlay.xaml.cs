#nullable enable
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Controls.Xaml;
using Serilog;
using Syncfusion.Maui.Core;

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
                logger.Debug("BusyOverlay.IsVisible: Setting from {OldValue} to {NewValue}", oldValue, value);
                SetValue(IsVisibleProperty, value);

                var opacity = value ? 1.0 : 0.0;
                var inputTransparent = !value; // When visible, don't allow input through (false), when hidden, allow input through (true)
                
                // Set opacity and InputTransparent on both the ContentView itself and the inner Grid
                // This ensures input is properly blocked/allowed at all levels
                this.InputTransparent = inputTransparent;
                
                if (overlayGrid != null)
                {
                    overlayGrid.Opacity = opacity;
                    overlayGrid.InputTransparent = inputTransparent;
                    logger.Debug("BusyOverlay.IsVisible: Set ContentView.InputTransparent to {InputTransparent}, overlayGrid.Opacity to {Opacity}, overlayGrid.InputTransparent to {InputTransparent} immediately", inputTransparent, opacity, inputTransparent);
                }

                // Force spinner to start/stop immediately when visibility changes
                // This ensures smooth animation without binding delays
                // Use Dispatch to ensure XAML is fully loaded
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
            logger.Debug("BusyOverlay.OnIsVisibleChanged: Property changed from {OldValue} to {NewValue} (opacity will be {Opacity}, inputTransparent will be {InputTransparent})",
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
        // When overlay is loaded, if it's visible, ensure opacity and InputTransparent are set and spinner starts immediately
        if (IsVisible)
        {
            logger.Debug("BusyOverlay: Loaded and visible, setting opacity, InputTransparent and starting spinner immediately");
            
            // Set InputTransparent on the ContentView itself (block input when visible)
            this.InputTransparent = false;
            
            // Set opacity and InputTransparent directly to ensure it's visible and blocks input immediately
            if (overlayGrid != null)
            {
                overlayGrid.Opacity = 1.0;
                overlayGrid.InputTransparent = false; // When visible, block input
                logger.Debug("BusyOverlay: Set ContentView.InputTransparent to false, overlayGrid.Opacity to 1.0, overlayGrid.InputTransparent to false on load");
            }
            
            // Start spinner
            if (busyIndicator != null)
            {
                busyIndicator.IsRunning = true;
            }
        }
        else
        {
            // When overlay is loaded but not visible, ensure it allows input through
            // Set InputTransparent on the ContentView itself (allow input when hidden)
            this.InputTransparent = true;
            
            if (overlayGrid != null)
            {
                overlayGrid.Opacity = 0.0;
                overlayGrid.InputTransparent = true; // When hidden, allow input through
                logger.Debug("BusyOverlay: Loaded but not visible, set ContentView.InputTransparent to true, overlayGrid.Opacity to 0.0, overlayGrid.InputTransparent to true on load");
            }
        }

        // Unsubscribe after first load
        Loaded -= OnBusyOverlayLoaded;
    }
}

