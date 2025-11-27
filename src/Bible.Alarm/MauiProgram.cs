using Bible.Alarm.Common.Interfaces.Battery;
using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Common.Interfaces.Platform;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Battery.Interfaces;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Network.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Database;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Battery;
using Bible.Alarm.Services.Database;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Network;
using Bible.Alarm.Services.Scheduler;
using Bible.Alarm.Services.UI;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels;
using Bible.Alarm.ViewModels.Bible;
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.Views;
using Bible.Alarm.Views.Bible;
using Bible.Alarm.Views.General;
using Bible.Alarm.Views.Music;
using Bible.Alarm.Views.Schedule;
using Bible.Alarm.Views.Shared;
using CommunityToolkit.Maui;
using Fluxor;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Threading;
#if IOS
using Bible.Alarm.Platforms.iOS.Services.Storage;
using Bible.Alarm.Platforms.iOS.Services.UI;
using Bible.Alarm.Platforms.iOS.Helpers;
using Bible.Alarm.Platforms.iOS.Services.Handlers;
using Bible.Alarm.Platforms.iOS.Services.Platform;
using Bible.Alarm.Platforms.iOS.Services.Media;
#endif

#if ANDROID
using Bible.Alarm.Platforms.Android.Services.Media;
using Bible.Alarm.Platforms.Android.Services.UI;
using Bible.Alarm.Platforms.Android.Services.Handlers;
using Bible.Alarm.Platforms.Android.Services.Helpers;
using Bible.Alarm.Platforms.Android.Services.Battery;
using Bible.Alarm.Platforms.Android.Services.Platform;
using Bible.Alarm.Platforms.Android.Services.Storage;
#if ANDROID
using Android.App;
#endif
using Microsoft.Maui.ApplicationModel;
#endif
#if WINDOWS
using Bible.Alarm.Platforms.Windows.Services.UI;
using Bible.Alarm.Platforms.Windows.Services.Handlers;
using Bible.Alarm.Platforms.Windows.Services.Media;
using Bible.Alarm.Platforms.Windows.Services.Storage;
using Bible.Alarm.Platforms.Windows.Services.Platform;
using Bible.Alarm.Platforms.Windows.Helpers;
using Windows.Media.Playback;
#endif

namespace Bible.Alarm;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
#if WINDOWS
        // Initialize Serilog for Windows before registering services
        // This ensures Log.Logger is properly configured before services try to use it
        var versionFinder = new WindowsVersionFinder();
        SerilogSetup.Initialize(versionFinder, [], "Windows", isLoggingEnabled: true);
        Log.Logger.Information("CreateMauiApp called!");
#elif ANDROID
        // Initialize Serilog for Android before registering services
        // This ensures Log.Logger is properly configured before services try to use it
        var versionFinder = AndroidVersionFinder.Default;
        SerilogSetup.Initialize(versionFinder, [], "Android", isLoggingEnabled: true);
        Log.Logger.Information("CreateMauiApp called!");
#elif IOS
        // Initialize Serilog for iOS before registering services
        // This ensures Log.Logger is properly configured before services try to use it
        var versionFinder = iOSVersionFinder.Default;
        SerilogSetup.Initialize(versionFinder, [], "iOS", isLoggingEnabled: true);
        Log.Logger.Information("CreateMauiApp called!");
#endif

        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkitMediaElement()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont(AppConstants.AppSettings.DefaultFontFileName, AppConstants.AppSettings.DefaultFontResourceName);
#if WINDOWS
                fonts.AddFont("Platforms/Windows/Assets/Fonts/Font Awesome 5 Free-Solid-900.otf", "FontAwesomeSolid");
#else
                fonts.AddFont("Resources/Fonts/FontAwesome5Free_Solid_900.otf", "FontAwesomeSolid");
#endif
            });

        // Register services
        ServiceRegistrationHelper.RegisterServices(builder.Services);

        var app = builder.Build();

        // Initialize Fluxor store
        var store = app.Services.GetRequiredService<IStore>();
        // Store initialization happens automatically, but we ensure it's ready
        store.InitializeAsync().GetAwaiter().GetResult();
        ReduxContainer.Store = store;

        return app;
    }


    private static readonly SemaphoreSlim BootstrapLock = new(1, 1);
    private static volatile bool BootstrapCompleted = false;

    /// <summary>
    /// Initializes platform-specific bootstrap.
    /// This should be called after MauiApp is created to ensure databases and services are initialized.
    /// Thread-safe: ensures bootstrap runs only once, even if called from multiple entry points concurrently.
    /// </summary>
    /// <param name="services">The service provider</param>
    /// <param name="isForeground">If true, runs bootstrap on a background Task. If false, runs synchronously.</param>
    public static void InitializePlatformBootstrap(IServiceProvider services, bool isForeground = false)
    {
        // Fast path: if bootstrap already completed, return immediately
        if (BootstrapCompleted)
        {
            return;
        }

        // Try to acquire lock without blocking (timeout = 0)
        var lockAcquired = ConcurrencyHelper.ExecuteAsync(BootstrapLock, async () =>
        {
            // Double-check if completed while waiting for lock
            if (BootstrapCompleted)
            {
                return;
            }

            if (isForeground)
            {
                // Run bootstrap as a background job for foreground launches to avoid blocking UI
                // Fire and forget - don't await
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await RunBootstrap(services).ConfigureAwait(false);
                        BootstrapCompleted = true;
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Error(ex, "Error in background bootstrap initialization");
                    }
                });
            }
            else
            {
                // Run bootstrap synchronously for background services/jobs
                await RunBootstrap(services).ConfigureAwait(false);
                BootstrapCompleted = true;
            }
        }, timeoutMs: 0).GetAwaiter().GetResult();

        // If lock wasn't acquired (timeout = 0 means try without waiting)
        if (!lockAcquired)
        {
            if (isForeground)
            {
                // For foreground, don't block - just return and let the other bootstrap complete
                return;
            }
            else
            {
                // For background services, wait for the lock and check if bootstrap completed
                ConcurrencyHelper.ExecuteAsync(BootstrapLock, async () =>
                {
                    // Double-check if completed while waiting
                    if (BootstrapCompleted)
                    {
                        return;
                    }
                    // If we got here, the previous bootstrap failed or was interrupted
                    // Continue to run bootstrap
                    await RunBootstrap(services).ConfigureAwait(false);
                    BootstrapCompleted = true;
                }).GetAwaiter().GetResult();
            }
        }
    }

    private static async Task RunBootstrap(IServiceProvider services)
    {
        try
        {
            var logger = services.GetRequiredService<ILogger>();

#if ANDROID
            // Android bootstrap initialization
            // Try Platform.CurrentActivity first, fallback to AndroidApplication.Context
            var context = Platform.CurrentActivity?.ApplicationContext ?? Android.App.Application.Context;
            var application = Platform.CurrentActivity?.Application ?? Android.App.Application.Context as Android.App.Application;

            if (context != null)
            {
                await AndroidBootstrapHelper.Initialize(logger, context, application).ConfigureAwait(false);
            }
            else
            {
                logger.Warning("Android context not available for bootstrap initialization");
            }
#elif IOS
            // iOS bootstrap initialization
            await iOSBootstrapHelper.Initialize(logger, isForeground: true).ConfigureAwait(false);
#elif WINDOWS
            // Windows bootstrap initialization
            await WindowsBootstrapHelper.Initialize(logger, isForeground: true).ConfigureAwait(false);
#endif
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Error in InitializePlatformBootstrap");
        }
    }
}