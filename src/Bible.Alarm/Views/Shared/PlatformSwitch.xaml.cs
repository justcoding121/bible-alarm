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
            // Update iOS SfSwitch
            if (platformSwitch.iOSSwitch != null)
            {
                platformSwitch.iOSSwitch.IsOn = isToggled;
            }
        }
    }


    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        // Set up event handlers after the view is loaded
        if (WinUISwitch != null)
        {
            WinUISwitch.Toggled += (_, e) => IsToggled = e.Value;
        }
        if (AndroidSwitch != null)
        {
            AndroidSwitch.StateChanged += (_, _) => IsToggled = AndroidSwitch.IsOn ?? false;
        }
        if (iOSSwitch != null)
        {
            iOSSwitch.StateChanged += (_, _) => IsToggled = iOSSwitch.IsOn ?? false;
        }
    }

    protected override void OnPropertyChanged(string propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        // Sync Scale property to child controls (for Android and iOS SfSwitch)
        if (propertyName == nameof(Scale))
        {
            if (AndroidSwitch != null)
            {
                AndroidSwitch.Scale = Scale;
            }
            if (iOSSwitch != null)
            {
                iOSSwitch.Scale = Scale;
            }
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

