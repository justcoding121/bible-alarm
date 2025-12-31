#nullable enable
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;
using Syncfusion.Maui.Core;

namespace Bible.Alarm.Views.Shared;

public partial class BusyOverlay : ContentView
{
    private static readonly ILogger logger = Log.ForContext<BusyOverlay>();

    public static readonly BindableProperty IsVisibleProperty = BindableProperty.Create(
        nameof(IsVisible),
        typeof(bool),
        typeof(BusyOverlay),
        false,
        BindingMode.OneWay,
        propertyChanged: OnIsVisibleChanged);

    public bool IsVisible
    {
        get => (bool)GetValue(IsVisibleProperty);
        set
        {
            var oldValue = (bool)GetValue(IsVisibleProperty);
            if (oldValue != value)
            {
                logger.Debug("BusyOverlay.IsVisible: Setting from {OldValue} to {NewValue}", oldValue, value);
                SetValue(IsVisibleProperty, value);
                
                // Force spinner to start/stop immediately when visibility changes
                // This ensures smooth animation without binding delays
                // Use BeginInvoke to ensure XAML is fully loaded
                if (value)
                {
                    // Wait for next UI cycle to ensure XAML is fully loaded
                    MainThread.BeginInvokeOnMainThread(() =>
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
            logger.Debug("BusyOverlay.OnIsVisibleChanged: Property changed from {OldValue} to {NewValue} (opacity will be {Opacity})", 
                oldValue, newBoolValue, newBoolValue ? 1.0 : 0.0);
            
            // Opacity binding will handle the visual update automatically
            // No need to invalidate measure or manipulate IsVisible since element stays in visual tree
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
        // When overlay is loaded, if it's visible, ensure spinner starts immediately
        if (IsVisible && busyIndicator != null)
        {
            logger.Debug("BusyOverlay: Loaded and visible, starting spinner immediately");
            busyIndicator.IsRunning = true;
        }
        
        // Unsubscribe after first load
        Loaded -= OnBusyOverlayLoaded;
    }
}

