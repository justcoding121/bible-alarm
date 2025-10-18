using Bible.Alarm.UI.Views;
using CommunityToolkit.Maui;

namespace Bible.Alarm;

public static class MauiProgramExtensions
{
    public static MauiAppBuilder UseSharedMauiApp(this MauiAppBuilder builder)
    {
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkitMediaElement();

        // MAUI app configuration is complete
        // Entry points are handled by the MAUI framework automatically


        return builder;
    }
}
