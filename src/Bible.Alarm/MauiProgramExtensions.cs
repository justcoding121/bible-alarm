using CommunityToolkit.Maui;

namespace Bible.Alarm;

public static class MauiProgramExtensions
{
    public static MauiAppBuilder UseSharedMauiApp(this MauiAppBuilder builder)
    {
#pragma warning disable CA1416
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkitMediaElement();
#pragma warning restore CA1416

        // MAUI app configuration is complete
        // Entry points are handled by the MAUI framework automatically


        return builder;
    }
}