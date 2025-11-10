using Bible.Alarm.Common;
using Bible.Alarm.Database;
using Bible.Alarm.Stores;
using CommunityToolkit.Maui;
using Fluxor;
using Bible.Alarm.Services;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Network;
using Bible.Alarm.Services.Scheduler;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.ViewModels;
using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.Views.Bible;
using Bible.Alarm.Views.Music;
// using Bible.Alarm.Views.Schedule; // Schedule is a type, not a namespace
using Bible.Alarm.Views.General;
// using Bible.Alarm.Views.Shared; // Shared is a folder, not a namespace
using Bible.Alarm.Common.Interfaces.Network;
using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Common.Interfaces.Battery;
using Bible.Alarm.Common.Interfaces.Platform;
using Bible.Alarm.Common.Interfaces.Scheduler;
using Bible.Alarm.Common.Interfaces.Storage;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Database.Migrations;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Views.Schedule;
using Bible.Alarm.Views.Shared;
using Bible.Alarm.ViewModels.Bible;
using Bible.Alarm.ViewModels.Music;
using Microsoft.EntityFrameworkCore;
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
#endif
#if WINDOWS
using Bible.Alarm.Platforms.Windows.Services.UI;
using Bible.Alarm.Platforms.Windows.Services.Handlers;
using Bible.Alarm.Platforms.Windows.Services.Media;
using Bible.Alarm.Platforms.Windows.Services.Storage;
using Bible.Alarm.Platforms.Windows.Services.Platform;
using Bible.Alarm.Platforms.Windows.Helpers;
#endif

namespace Bible.Alarm;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        System.Diagnostics.Debug.WriteLine("CreateMauiApp called!");
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkitMediaElement()
            .ConfigureFonts(fonts => 
            { 
                fonts.AddFont(AppConstants.AppSettings.DefaultFontFileName, AppConstants.AppSettings.DefaultFontResourceName);
#if WINDOWS
                fonts.AddFont("Platforms/Windows/Assets/Fonts/Font Awesome 5 Free-Solid-900.otf", "FontAwesomeSolid");
#endif
            });

        // Register services
        RegisterServices(builder.Services);

        var app = builder.Build();

        // Initialize Fluxor store
        var store = app.Services.GetRequiredService<Fluxor.IStore>();
        ReduxContainer.Store = store;

        // Initialize the global service provider for access from multiple entry points
        ServiceProviderManager.Initialize(app.Services);

        // Create HomeViewModel early to ensure it subscribes before Init message is published
        _ = app.Services.GetRequiredService<HomeViewModel>();

        // Initialize platform-specific bootstrap helpers
        InitializePlatformBootstrap(app.Services);

        return app;
    }

    /// <summary>
    /// Ensures the DI container is initialized for background services.
    /// This method is safe to call multiple times and will only initialize once.
    /// </summary>
    public static void EnsureDiContainerInitialized()
    {
        if (!ServiceProviderManager.IsInitialized)
        {
            System.Diagnostics.Debug.WriteLine("Initializing DI container for background service...");
            
            // Create a minimal MauiApp instance to initialize the DI container
            CreateMauiApp();
            
            // The app instance is not used, but the DI container is now initialized
            // ServiceProviderManager.Initialize() was called in CreateMauiApp()
            System.Diagnostics.Debug.WriteLine("DI container initialized for background service.");
        }
    }

    private static void RegisterServices(IServiceCollection services)
    {
        // Register platform-specific HttpMessageHandler
        #if ANDROID
        services.AddSingleton<HttpMessageHandler, Xamarin.Android.Net.AndroidMessageHandler>();
        #elif IOS
        services.AddSingleton<HttpMessageHandler, System.Net.Http.HttpClientHandler>();
        #elif WINDOWS
        services.AddSingleton<HttpMessageHandler, HttpClientHandler>();
#endif

        // Register common services
        RegisterCommonServices(services);

        // Register ViewModels
        RegisterViewModels(services);

        // Register UI components
        RegisterUiComponents(services);
    }

    private static void RegisterCommonServices(IServiceCollection services)
    {
        // Register Fluxor
        services.AddFluxor(options => options.ScanAssemblies(typeof(MauiProgram).Assembly));
        
        // Register logging
        services.AddSingleton<Serilog.ILogger>(sp => Serilog.Log.Logger);
        
        // Register core services that don't have platform dependencies
        services.AddSingleton<IDownloadService, DownloadService>();
        services.AddSingleton<MediaIndexService>();
        services.AddSingleton<MediaService>();
        services.AddSingleton<IMediaCacheService, MediaCacheService>();
        services.AddSingleton<IPlaylistService, PlaylistService>();
        services.AddSingleton<IAlarmService, AlarmService>();
        services.AddSingleton<INetworkStatusService, NetworkStatusService>();
        services.AddSingleton<IMediaElementAudioService, MediaElementAudioService>();
        services.AddSingleton<IPlaybackService, PlaybackService>();
        services.AddSingleton<SchedulerService>();
        services.AddSingleton<ISchedulerService>(sp => sp.GetRequiredService<SchedulerService>());
        services.AddSingleton<IMediaIndexService>(sp => sp.GetRequiredService<MediaIndexService>());
        
        // Register platform-specific version finder
        #if ANDROID
        services.AddSingleton<IVersionFinder, VersionFinder>();
        #elif IOS
        services.AddSingleton<IVersionFinder, VersionFinder>();
        #elif WINDOWS
        services.AddSingleton<IVersionFinder, WindowsVersionFinder>();
        #endif

        // Register platform-specific services
        #if ANDROID
        services.AddSingleton<INotificationService, DroidNotificationService>();
        services.AddSingleton<IToastService, DroidToastService>();
        services.AddSingleton<IAndroidAlarmHandler, AndroidAlarmHandler>();
        services.AddSingleton<IStorageService, AndroidStorageService>();
        services.AddSingleton<IBatteryOptimizationManager, BatteryOptimizationManager>();
        services.AddSingleton<IPreviewPlayService, PreviewPlayService>();
        #elif IOS
        services.AddSingleton<INotificationService, IOsNotificationService>();
        services.AddSingleton<IToastService, IOsToastService>();
        services.AddSingleton<IStorageService, IOsStorageService>();
        services.AddSingleton<IPreviewPlayService, PreviewPlayService>();
        services.AddSingleton<IOsAlarmHandler>();
        #elif WINDOWS
        services.AddSingleton<INotificationService, WindowsNotificationService>();
        services.AddSingleton<IToastService, WindowsToastService>();
        services.AddSingleton<IStorageService, WindowsStorageService>();
        services.AddSingleton<Windows.Media.Playback.MediaPlayer>(sp => new Windows.Media.Playback.MediaPlayer());
        services.AddSingleton<IPreviewPlayService, PreviewPlayService>();
        services.AddSingleton<WindowsAlarmHandler>();
        #endif

        // Register database contexts
        services.AddDbContext<ScheduleDbContext>((sp, options) =>
        {
            var storageService = sp.GetRequiredService<IStorageService>();
            var databasePath = Path.Combine(storageService.StorageRoot, AppConstants.Database.ScheduleDatabaseFileName);
            options.UseSqlite(string.Format(AppConstants.Database.ScheduleDatabaseConnectionStringFormat, databasePath));
        });

        services.AddDbContext<MediaDbContext>((sp, options) =>
        {
            var storageService = sp.GetRequiredService<IStorageService>();
            var databasePath = Path.Combine(storageService.StorageRoot, AppConstants.Database.MediaIndexDatabaseFileName);
            options.UseSqlite(string.Format(AppConstants.Database.MediaIndexDatabaseConnectionStringFormat, databasePath));
        });

        // Register TaskScheduler for compatibility - use default scheduler instead of UI context
        services.AddSingleton<TaskScheduler>(sp => TaskScheduler.Default);
    }

    private static void RegisterViewModels(IServiceCollection services)
    {
        services.AddTransient<HomeViewModel>();
        services.AddTransient<ScheduleViewModel>();
        services.AddTransient<MusicSelectionViewModel>();
        services.AddTransient<SongBookSelectionViewModel>();
        services.AddTransient<TrackSelectionViewModel>();
        services.AddTransient<BibleSelectionViewModel>();
        services.AddTransient<BookSelectionViewModel>();
        services.AddTransient<ChapterSelectionViewModel>();
        services.AddTransient<AlarmViewModal>();
        services.AddSingleton<MediaProgressViewModal>();
    }

    private static void RegisterUiComponents(IServiceCollection services)
    {
        services.AddTransient<Home>();
        services.AddTransient<Schedule>();
        services.AddTransient<MusicSelection>();
        services.AddTransient<SongBookSelection>();
        services.AddTransient<TrackSelection>();
        services.AddTransient<BibleSelection>();
        services.AddTransient<BookSelection>();
        services.AddTransient<ChapterSelection>();
        services.AddTransient<LanguageModal>();
        services.AddTransient<AlarmModal>();
        services.AddTransient<BatteryOptimizationExclusionModal>();
        services.AddTransient<NumberOfChaptersModal>();
        services.AddTransient<MediaProgressModal>();
    }

    private static void InitializePlatformBootstrap(IServiceProvider services)
    {
        var logger = services.GetRequiredService<Serilog.ILogger>();
        
        #if ANDROID
        // Android bootstrap initialization
        var context = Platform.CurrentActivity?.ApplicationContext ?? Android.App.Application.Context;
        var application = Platform.CurrentActivity?.Application;
        Bible.Alarm.Platforms.Android.Services.Helpers.BootstrapHelper.Initialize(logger, context, application);
        #elif IOS
        // iOS bootstrap initialization
        Bible.Alarm.Platforms.iOS.Helpers.BootstrapHelper.Initialize(logger, isForeground: true);
        #elif WINDOWS
        // Windows bootstrap initialization
        Bible.Alarm.Platforms.Windows.Helpers.BootstrapHelper.Initialize(logger, isForeground: true);
        #endif
    }
}