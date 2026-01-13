#nullable enable
using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml.Controls;

namespace Bible.Alarm.Platforms.Windows.Handlers;

/// <summary>
/// Custom Switch handler for Windows to override the default MinWidth of 154px.
/// WinUI ToggleSwitch has a default MinWidth that causes layout issues in nested Grids.
/// </summary>
public class SwitchHandler : Microsoft.Maui.Handlers.SwitchHandler
{
    protected override ToggleSwitch CreatePlatformView()
    {
        var toggleSwitch = base.CreatePlatformView();
        // Override the default MinWidth of 154px to allow the switch to render at its natural width (~48px)
        toggleSwitch.MinWidth = 0;
        return toggleSwitch;
    }

    protected override void ConnectHandler(ToggleSwitch platformView)
    {
        base.ConnectHandler(platformView);
        // Ensure MinWidth is set to 0 after connection
        // This overrides the default WinUI ToggleSwitch MinWidth of 154px
        platformView.MinWidth = 0;
    }
}
