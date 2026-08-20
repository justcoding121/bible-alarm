#nullable enable

#if WINDOWS
using Microsoft.UI.Xaml.Controls;
#endif
using Bible.Alarm.Shared.Constants;
using Serilog;
using Syncfusion.Maui.Buttons;

namespace Bible.Alarm.Views.Shared;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class PlatformSwitch : ContentView
{
    public static readonly BindableProperty IsToggledProperty =
        BindableProperty.Create(nameof(IsToggled), typeof(bool), typeof(PlatformSwitch), false, BindingMode.TwoWay,
            propertyChanged: OnIsToggledChanged);

    // Platform-specific switch controls (only one will be non-null at runtime)
    private Switch? winUISwitch = null;
    private SfSwitch? sfSwitch = null;


    public PlatformSwitch()
    {
        InitializeComponent();

        Scale = 1.0;
        HorizontalOptions = LayoutOptions.End;

        // Create platform-specific switch in code to avoid OnPlatform<View> issues
        InitializePlatformSwitch();
    }

    private void InitializePlatformSwitch()
    {
#if WINDOWS
        winUISwitch = new Switch
        {
            MinimumWidthRequest = 0,
            MaximumWidthRequest = 60,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Start
        };
        Content = winUISwitch;
#else
        sfSwitch = new SfSwitch
        {
            VerticalOptions = LayoutOptions.Center,
            Scale = Scale,
            HorizontalOptions = HorizontalOptions,
            SwitchSettings = new SwitchSettings
            {
                ThumbBackground = Color.FromArgb("#483D8B"),
                TrackBackground = Color.FromArgb("#9E9E9E")
            }
        };
        Content = sfSwitch;
#endif
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

            if (platformSwitch.winUISwitch != null)
            {
                platformSwitch.winUISwitch.IsToggled = isToggled;
            }

            if (platformSwitch.sfSwitch != null)
            {
                platformSwitch.sfSwitch.IsOn = isToggled;
            }
        }
    }


    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        UnsubscribeFromChildEvents();
        if (Handler != null)
        {
            SubscribeToChildEvents();
        }
    }

    private void SubscribeToChildEvents()
    {
        if (winUISwitch != null)
        {
            winUISwitch.Toggled += OnWinUISwitchToggled;
#if WINDOWS
            try
            {
                if (Handler?.PlatformView is Microsoft.UI.Xaml.Controls.ToggleSwitch toggleSwitch)
                    toggleSwitch.MinWidth = 0;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, AppConstants.Logging.MauiPlatformUiDiagnosticsLog.PlatformSwitchMinWidthSetFailedWinRtThreadRelease);
            }
#endif
        }

        if (sfSwitch != null)
        {
            sfSwitch.StateChanged += OnSfSwitchStateChanged;
        }
    }

    private void UnsubscribeFromChildEvents()
    {
        if (winUISwitch != null)
        {
            winUISwitch.Toggled -= OnWinUISwitchToggled;
        }

        if (sfSwitch != null)
        {
            sfSwitch.StateChanged -= OnSfSwitchStateChanged;
        }
    }

    private void OnWinUISwitchToggled(object? sender, object e)
    {
        if (winUISwitch != null)
        {
            IsToggled = winUISwitch.IsToggled;
        }
    }

    private void OnSfSwitchStateChanged(object? sender, SwitchStateChangedEventArgs e)
    {
        if (sfSwitch != null)
        {
            IsToggled = sfSwitch.IsOn ?? false;
        }
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        // Sync Scale / HorizontalOptions to child control (Android and iOS SfSwitch)
        if (sfSwitch != null)
        {
            if (propertyName == nameof(Scale))
            {
                sfSwitch.Scale = Scale;
            }
            else if (propertyName == nameof(HorizontalOptions))
            {
                sfSwitch.HorizontalOptions = HorizontalOptions;
            }
        }
    }
}

