using CommunityToolkit.Maui;
using Bible.Alarm.Services;
using Bible.Alarm.Services.Contracts;
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
using Bible.Alarm.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkitMediaElement()
            .ConfigureFonts(fonts => { fonts.AddFont(AppConstants.AppSettings.DefaultFontFileName, AppConstants.AppSettings.DefaultFontResourceName); });

        // Register services
        RegisterServices(builder.Services);

        var app = builder.Build();

        // Initialize the global service provider for access from multiple entry points
        ServiceProviderManager.Initialize(app.Services);

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
        services.AddSingleton<ILogger>(sp => Serilog.Log.Logger);
        
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
        services.AddSingleton<IVersionFinder, Bible.Alarm.Droid.Services.Platform.VersionFinder>();
        #elif IOS
        services.AddSingleton<IVersionFinder, Bible.Alarm.iOS.Services.Platform.VersionFinder>();
        #elif WINDOWS
        services.AddSingleton<IVersionFinder, Bible.Alarm.Services.Windows.Platform.UwpVersionFinder>();
        #endif

        // Register platform-specific services
        #if ANDROID
        services.AddSingleton<INotificationService, Bible.Alarm.Services.Droid.DroidNotificationService>();
        services.AddSingleton<IToastService, Bible.Alarm.Services.Droid.DroidToastService>();
        services.AddSingleton<IAndroidAlarmHandler, Bible.Alarm.Droid.Services.Handlers.AndroidAlarmHandler>();
        services.AddSingleton<IStorageService, Bible.Alarm.Droid.Services.Storage.AndroidStorageService>();
        services.AddSingleton<IBatteryOptimizationManager, Bible.Alarm.Droid.Services.Battery.BatteryOptimizationManager>();
        services.AddSingleton<IPreviewPlayService, Bible.Alarm.Services.Droid.PreviewPlayService>();
        #elif IOS
        services.AddSingleton<INotificationService, Bible.Alarm.Services.iOS.IOsNotificationService>();
        services.AddSingleton<IToastService, Bible.Alarm.Services.iOS.IOsToastService>();
        services.AddSingleton<IStorageService, Bible.Alarm.Droid.Services.Storage.IOsStorageService>();
        services.AddSingleton<IPreviewPlayService, Bible.Alarm.Services.iOS.PreviewPlayService>();
        services.AddSingleton<Bible.Alarm.iOS.Services.Handlers.IOsAlarmHandler>();
        #elif WINDOWS
        services.AddSingleton<INotificationService, Bible.Alarm.Services.Windows.UwpNotificationService>();
        services.AddSingleton<IToastService, Bible.Alarm.Services.Windows.UwpToastService>();
        services.AddSingleton<IStorageService, Bible.Alarm.Services.Windows.Storage.UwpStorageService>();
        services.AddSingleton<IPreviewPlayService, Bible.Alarm.Services.Windows.PreviewPlayService>();
        services.AddSingleton<Bible.Alarm.Services.Windows.Handlers.UwpAlarmHandler>();
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

        // Register TaskScheduler for compatibility
        services.AddSingleton<TaskScheduler>(sp => TaskScheduler.FromCurrentSynchronizationContext());
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
}