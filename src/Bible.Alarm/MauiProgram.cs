using Bible.Alarm.Common.Mvvm;
using Bible.Alarm.Services.Infrastructure;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;

namespace Bible.Alarm;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp(IContainer container = null)
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkitMediaElement()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // Register services
        // builder.Services.AddMauiBlazorWebView(); // Not needed for this app

#if DEBUG
        // builder.Services.AddLogging(configure => configure.AddDebug()); // Not needed for this app
#endif

        // Register the container if provided
        if (container != null)
        {
            builder.Services.AddSingleton(container);
            
            // Initialize platform-specific services
            // Note: Platform-specific IocSetup calls are handled in entry points
            // (iOS AppDelegate, Android MainActivity, Windows App.xaml.cs)
        }
        
        // Register the App class with the container
        builder.Services.AddSingleton<App>(sp => 
        {
            if (container != null)
            {
                return new App(container);
            }
            // Create a default container if none provided
            var defaultContainer = new Container(new Dictionary<string, object>());
            return new App(defaultContainer);
        });

        return builder.Build();
    }
}
