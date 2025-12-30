#nullable enable
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;

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

    public BusyOverlay()
    {
        InitializeComponent();
    }
}

