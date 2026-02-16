#if IOS
using Bible.Alarm.Platforms.iOS.Services.Platform;
using Bible.Alarm.Platforms.iOS.Handlers;
#endif

#if ANDROID
using Bible.Alarm.Platforms.Android.Handlers;
using Bible.Alarm.Platforms.Android.Services.Platform;
#endif
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.UI;
using Bible.Alarm.Services.UI.Interfaces;
using CommunityToolkit.Maui;
using Serilog;
using Syncfusion.Licensing;
using Syncfusion.Maui.Core.Hosting;
#if WINDOWS
using Bible.Alarm.Platforms.Windows.Services.Platform;
using Bible.Alarm.Platforms.Windows.Handlers;
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
        var versionFinder = IOsVersionFinder.Default;
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
                handlers.AddHandler<Entry, EntryHandler>();
                handlers.AddHandler<TimePicker, TimePickerHandler>();
            })
#elif IOS
            .ConfigureMauiHandlers(handlers =>
            {
                handlers.AddHandler<Entry, EntryHandler>();
                handlers.AddHandler<SearchBar, SearchBarHandler>();
            })
#elif WINDOWS
            .ConfigureMauiHandlers(handlers =>
            {
                handlers.AddHandler<Microsoft.Maui.Controls.Switch, SwitchHandler>();
            })
#endif
            .ConfigureFonts(fonts =>
            {
                // Font Awesome 7 fonts - use underscores in filenames for Android compatibility
                // This works universally across all platforms (Android, iOS, Windows)
                Log.Logger.Debug("Registering Font Awesome fonts");
                fonts.AddFont("fa_solid_900.otf", "FontAwesomeSolid");
                fonts.AddFont("fa_regular_400.otf", "FontAwesomeRegular");
                fonts.AddFont("fa_brands_400.otf", "FontAwesomeBrands");
                Log.Logger.Debug("Font Awesome fonts registered successfully");
            });

        // Register services
#if DEBUG
        var serviceRegStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
        ServiceRegistrationHelper.RegisterServices(builder.Services);
#if DEBUG
        var serviceRegElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - serviceRegStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        Log.Logger.Information("[BOOTSTRAP] Service registration completed in {ElapsedMs:F2}ms", serviceRegElapsed);
#endif

#if DEBUG
        var buildStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
        var app = builder.Build();
#if DEBUG
        var buildElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - buildStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        Log.Logger.Information("[BOOTSTRAP] MauiApp.Build() completed in {ElapsedMs:F2}ms", buildElapsed);
#endif

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
    public static void InitializePlatformBootstrap(IServiceProvider services, bool isForeground = false) => BootstrapHelper.InitializePlatformBootstrap(services, isForeground);

    /// <summary>
    /// Synchronously waits for bootstrap to complete before allowing database access.
    /// Use this method when you must wait synchronously (e.g., in framework override methods).
    /// Does NOT throw on timeout — logs a warning and returns gracefully.
    /// </summary>
    public static void WaitForBootstrap(int timeoutMs = 60000) => BootstrapHelper.WaitForBootstrap(timeoutMs);

    /// <summary>
    /// Waits for bootstrap to complete before allowing database access.
    /// This ensures database migrations are finished before services use the database.
    /// Does NOT throw on timeout — logs a warning and returns gracefully.
    /// </summary>
    public static async Task WaitForBootstrapAsync(int timeoutMs = 60000) => await BootstrapHelper.WaitForBootstrapAsync(timeoutMs);
}
