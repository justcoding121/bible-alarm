using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Services.UI;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Shared.Constants;
using CommunityToolkit.Maui;
using Syncfusion.Licensing;
using Syncfusion.Maui.Core.Hosting;
using Microsoft.EntityFrameworkCore;
using Serilog;
#if IOS
using Bible.Alarm.Platforms.iOS.Services.Storage;
using Bible.Alarm.Platforms.iOS.Services.UI;
using Bible.Alarm.Platforms.iOS.Helpers;
using Bible.Alarm.Platforms.iOS.Services.Handlers;
using Bible.Alarm.Platforms.iOS.Services.Platform;
using Bible.Alarm.Platforms.iOS.Services.Media;
using Bible.Alarm.Platforms.iOS.Handlers;
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
        // Register Syncfusion license key (must be done before any Syncfusion controls are used)
        // License key is embedded at build time from SYNCFUSION_LICENSE_KEY environment variable.
        // The AppSettings class is generated during build with the embedded license key.
        if (!string.IsNullOrEmpty(AppSettings.SyncfusionLicenseKey))
        {
            SyncfusionLicenseProvider.RegisterLicense(AppSettings.SyncfusionLicenseKey);
        }

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
            .ConfigureSyncfusionCore()
#if ANDROID
            .ConfigureMauiHandlers(handlers =>
            {
                handlers.AddHandler<Microsoft.Maui.Controls.Entry, Bible.Alarm.Platforms.Android.Handlers.EntryHandler>();
                handlers.AddHandler<Microsoft.Maui.Controls.TimePicker, Bible.Alarm.Platforms.Android.Handlers.TimePickerHandler>();
            })
#elif IOS
            .ConfigureMauiHandlers(handlers =>
            {
                handlers.AddHandler<Microsoft.Maui.Controls.SearchBar, SearchBarHandler>();
            })
#endif
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont(AppConstants.AppSettings.DefaultFontFileName, AppConstants.AppSettings.DefaultFontResourceName);

                // Font Awesome 7 fonts - use underscores in filenames for Android compatibility
                // This works universally across all platforms (Android, iOS, Windows)
                Log.Logger.Debug("Registering Font Awesome fonts");
                fonts.AddFont("fa_solid_900.otf", "FontAwesomeSolid");
                fonts.AddFont("fa_regular_400.otf", "FontAwesomeRegular");
                fonts.AddFont("fa_brands_400.otf", "FontAwesomeBrands");
                Log.Logger.Debug("Font Awesome fonts registered successfully");
            });

        // Register services
        ServiceRegistrationHelper.RegisterServices(builder.Services);

        var app = builder.Build();

        // Initialize FontServiceHelper for XAML binding (must be done before App.xaml resources are accessed)
        var fontService = app.Services.GetRequiredService<IFontService>();
        FontServiceHelper.Initialize(fontService);

        // Fluxor store initialization is now handled in CommonBootstrapHelper.VerifyServices()
        // This ensures it's initialized asynchronously as part of the bootstrap process

        return app;
    }


    /// <summary>
    /// Initializes platform-specific bootstrap.
    /// This should be called after MauiApp is created to ensure databases and services are initialized.
    /// Thread-safe: ensures bootstrap runs only once, even if called from multiple entry points concurrently.
    /// </summary>
    public static void InitializePlatformBootstrap(IServiceProvider services, bool isForeground = false)
    {
        BootstrapHelper.InitializePlatformBootstrap(services, isForeground);
    }

    /// <summary>
    /// Synchronously waits for bootstrap to complete before allowing database access.
    /// Use this method when you must wait synchronously (e.g., in framework override methods).
    /// </summary>
    public static void WaitForBootstrap(int timeoutMs = 30000)
    {
        BootstrapHelper.WaitForBootstrap(timeoutMs);
    }

    /// <summary>
    /// Waits for bootstrap to complete before allowing database access.
    /// This ensures database migrations are finished before services use the database.
    /// </summary>
    public static async Task WaitForBootstrapAsync(int timeoutMs = 30000)
    {
        await BootstrapHelper.WaitForBootstrapAsync(timeoutMs);
    }
}
