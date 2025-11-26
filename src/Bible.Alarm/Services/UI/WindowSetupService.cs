#nullable enable
using Bible.Alarm.Views;
using Bible.Alarm.Views.General;
using Microsoft.Maui.Controls;

namespace Bible.Alarm.Services.UI;

public class WindowSetupService(IServiceProvider serviceProvider)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public Window CreateWindow(IActivationState? activationState)
    {
        var bootstrapPage = _serviceProvider.GetRequiredService<BootstrapPage>();
        var navigationPage = new NavigationPage(bootstrapPage)
        {
            BarBackgroundColor = Colors.Transparent,
            BarTextColor = Colors.White
        };

        NavigationPage.SetHasNavigationBar(bootstrapPage, false);

        var window = new Window(navigationPage);

#if WINDOWS
        // Set window size preferences (matching stable code)
        window.Width = 400;
        window.Height = 700;
        window.MinimumWidth = 400;
        window.MinimumHeight = 700;
#endif

        return window;
    }
}

