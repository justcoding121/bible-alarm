using Microsoft.Maui.Controls;
using Syncfusion.Maui.Buttons;

namespace Bible.Alarm.Views.Shared;

public partial class PlatformSwitch : ContentView
{
    public static readonly BindableProperty IsToggledProperty =
        BindableProperty.Create(nameof(IsToggled), typeof(bool), typeof(PlatformSwitch), false, BindingMode.TwoWay,
            propertyChanged: OnIsToggledChanged);

    public PlatformSwitch()
    {
        InitializeComponent();
        // Set default values for inherited properties
        Scale = 1.0;
        HorizontalOptions = LayoutOptions.End;
    }

    public bool IsToggled
    {
        get => (bool)GetValue(IsToggledProperty);
        set => SetValue(IsToggledProperty, value);
    }

    private static void OnIsToggledChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is PlatformSwitch platformSwitch)
        {
            var isToggled = (bool)newValue;
            
            // Update WinUI Switch
            if (platformSwitch.WinUISwitch != null)
            {
                platformSwitch.WinUISwitch.IsToggled = isToggled;
            }
            
            // Update Android SfSwitch
            if (platformSwitch.AndroidSwitch != null)
            {
                platformSwitch.AndroidSwitch.IsOn = isToggled;
            }
            // Update iOS native Switch
            if (platformSwitch.iOSSwitch != null)
            {
                platformSwitch.iOSSwitch.IsToggled = isToggled;
            }
        }
    }


    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        
        // Set up event handlers after the view is loaded
        if (WinUISwitch != null)
        {
            WinUISwitch.Toggled += (s, e) => IsToggled = e.Value;
        }
        if (AndroidSwitch != null)
        {
            AndroidSwitch.StateChanged += (s, e) => IsToggled = AndroidSwitch.IsOn ?? false;
        }
        if (iOSSwitch != null)
        {
            iOSSwitch.Toggled += (s, e) => IsToggled = e.Value;
        }
    }

    protected override void OnPropertyChanged(string propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        
        // Sync Scale property to child controls (only for Android SfSwitch)
        if (propertyName == nameof(Scale))
        {
            if (AndroidSwitch != null)
            {
                AndroidSwitch.Scale = Scale;
            }
            // iOS native Switch doesn't support Scale property
        }
        
        // Sync HorizontalOptions property to child controls
        if (propertyName == nameof(HorizontalOptions))
        {
            if (WinUIBorder != null)
            {
                WinUIBorder.HorizontalOptions = HorizontalOptions;
            }
            if (WinUISwitch != null)
            {
                WinUISwitch.HorizontalOptions = HorizontalOptions;
            }
            if (AndroidSwitch != null)
            {
                AndroidSwitch.HorizontalOptions = HorizontalOptions;
            }
            if (iOSSwitch != null)
            {
                iOSSwitch.HorizontalOptions = HorizontalOptions;
            }
        }
    }
}

