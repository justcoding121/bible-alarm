using Bible.Alarm.Common;
using CommunityToolkit.Maui;
using Bible.Alarm.Services;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Network;
using Bible.Alarm.Services.Tasks;
using Bible.Alarm.ViewModels;
using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.UI;
using Bible.Alarm.UI.Views;
using Bible.Alarm.UI.Views.Bible;
using Bible.Alarm.UI.Views.Music;
// using Bible.Alarm.UI.Views.Schedule; // Schedule is a type, not a namespace
using Bible.Alarm.UI.Views.General;
// using Bible.Alarm.UI.Views.Shared; // Shared is a folder, not a namespace
using Bible.Alarm.Contracts.Network;
using Bible.Alarm.Contracts.Media;
using Bible.Alarm.Contracts.Battery;
using Bible.Alarm.Contracts.Platform;
using Bible.Alarm.Contracts.Scheduler;
using Bible.Alarm.Contracts.Storage;
using Bible.Alarm.Contracts.UI;
using Bible.Alarm.Services.Infrastructure.Media;
using Bible.Alarm.Services.Infrastructure.Schedule;
using Bible.Alarm.Services.Scheduler;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.UI.Services;
using Bible.Alarm.UI.Views.Schedule;
using Bible.Alarm.UI.Views.Shared;
using Bible.Alarm.ViewModels.Bible;
using Bible.Alarm.ViewModels.Music;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog;
using Bible.Alarm.Platforms.iOS.Services.Storage;
using Bible.Alarm.Platforms.iOS.Services.UI;
using Bible.Alarm.Platforms.iOS.Helpers;
using Bible.Alarm.Platforms.iOS.Services.Handlers;
using Bible.Alarm.Platforms.iOS.Services.Platform;
using Bible.Alarm.Platforms.iOS.Services.Media;
using Bible.Alarm.Platforms.Android.Services.Media;
using Bible.Alarm.Platforms.Android.Services.UI;
using Bible.Alarm.Platforms.Android.Services.Handlers;
using Bible.Alarm.Platforms.Android.Services.Helpers;
using Bible.Alarm.Platforms.Android.Services.Battery;
using Bible.Alarm.Platforms.Android.Services.Platform;
using Bible.Alarm.Platforms.Android.Services.Storage;
using Bible.Alarm.Platforms.Windows.Services.UI;
using Bible.Alarm.Platforms.Windows.Services.Handlers;
using Bible.Alarm.Platforms.Windows.Services.Media;
using Bible.Alarm.Platforms.Windows.Services.Storage;
using Bible.Alarm.Platforms.Windows.Services.Platform;
using Bible.Alarm.Platforms.Windows.Helpers;

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
            .ConfigureFonts(fonts => { fonts.AddFont(AppConstants.AppSettings.DefaultFontFileName, AppConstants.AppSettings.DefaultFontResourceName); });

// Debug logging is handled by Serilog

        // Register services
        RegisterServices(builder.Services);

        var app = builder.Build();

        // Initialize the global service provider for access from multiple entry points
        ServiceProviderManager.Initialize(app.Services);

        // Initialize platform-specific bootstrap helpers
        InitializePlatformBootstrap(app.Services);

        return app;
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
        services.AddSingleton<SchedulerTask>();
        services.AddSingleton<ISchedulerService>(sp => sp.GetRequiredService<SchedulerTask>());
        services.AddSingleton<IMediaIndexService>(sp => sp.GetRequiredService<MediaIndexService>());
        
        // Register platform-specific version finder
        #if ANDROID
        services.AddSingleton<IVersionFinder, VersionFinder>();
        #elif IOS
        services.AddSingleton<IVersionFinder, VersionFinder>();
        #elif WINDOWS
        services.AddSingleton<IVersionFinder, UwpVersionFinder>();
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
        services.AddSingleton<INotificationService, UwpNotificationService>();
        services.AddSingleton<IToastService, UwpToastService>();
        services.AddSingleton<IStorageService, UwpStorageService>();
        services.AddSingleton<IPreviewPlayService, PreviewPlayService>();
        services.AddSingleton<UwpAlarmHandler>();
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
        
        // Register NavigationService - will be properly initialized in App.xaml.cs
        services.AddSingleton<INavigationService>(sp => 
        {
            var logger = sp.GetRequiredService<Serilog.ILogger>();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            return new NavigationService(logger, null, scopeFactory);
        });
    }

    private static void RegisterViewModels(IServiceCollection services)
    {
        services.AddSingleton<HomeViewModel>();
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
        BootstrapHelper.Initialize(logger, context);
#elif IOS
        // iOS bootstrap initialization

<<<<<<< TODO: Unmerged change from project 'Bible.Alarm (net9.0-windows10.0.19041.0)', Before:
        Bible.Alarm.Services.iOS.Helpers.BootstrapHelper.Initialize(logger);
        #elif WINDOWS
        // Windows bootstrap initialization
=======
        BootstrapHelper.Initialize(logger);
#elif WINDOWS
        // Windows bootstrap initialization
>>>>>>> After
        BootstrapHelper.Initialize(logger);
        #elif WINDOWS
        // Windows bootstrap initialization
        BootstrapHelper.Initialize(logger);
        #endif
    }
}