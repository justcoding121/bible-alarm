// using Bible.Alarm.Views.Schedule; // Schedule is a type, not a namespace
// using Bible.Alarm.Views.Shared; // Shared is a folder, not a namespace

using Bible.Alarm.Common.Interfaces.Battery;
using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Common.Interfaces.Network;
using Bible.Alarm.Common.Interfaces.Platform;
using Bible.Alarm.Common.Interfaces.Scheduler;
using Bible.Alarm.Common.Interfaces.Storage;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Database;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Battery;
using Bible.Alarm.Services.Database;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Network;
using Bible.Alarm.Services.Scheduler;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI;
using Bible.Alarm.Common;
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
#endif
            });

        // Register services
        RegisterServices(builder.Services);

        var app = builder.Build();

        // Initialize Fluxor store
        var store = app.Services.GetRequiredService<IStore>();
        // Store initialization happens automatically, but we ensure it's ready
        store.InitializeAsync().GetAwaiter().GetResult();
        ReduxContainer.Store = store;

        return app;
    }

    private static void RegisterServices(IServiceCollection services)
    {
        // Register platform-specific HttpMessageHandler
#if ANDROID
        services.AddSingleton<HttpMessageHandler, HttpClientHandler>();
#elif IOS
        services.AddSingleton<HttpMessageHandler, HttpClientHandler>();
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
        services.AddSingleton(_ => Log.Logger);

        // Register core services that don't have platform dependencies
        services.AddSingleton<IDownloadService, DownloadService>();
        services.AddSingleton<MediaIndexService>();
        services.AddSingleton<MediaService>();
        services.AddSingleton<IMediaCacheService, MediaCacheService>();
        services.AddSingleton<IPlaylistService, PlaylistService>();
        services.AddSingleton<IAlarmService, AlarmService>();
        services.AddSingleton<IScheduleStateService, ScheduleStateService>();
        services.AddSingleton<ISchedulePlaybackService, SchedulePlaybackService>();
        services.AddSingleton<IScheduleDisplayService, ScheduleDisplayService>();
        services.AddSingleton<ISchedulePersistenceService, SchedulePersistenceService>();
        services.AddSingleton<IBibleNavigationService, BibleNavigationService>();
        services.AddSingleton<IMediaCacheSetupService, MediaCacheSetupService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IScheduleSelectionService, ScheduleSelectionService>();
        services.AddSingleton<INetworkStatusService, NetworkStatusService>();
        services.AddSingleton<IMediaElementAudioService, MediaElementAudioService>();
        services.AddSingleton<IPlaybackService, PlaybackService>();
        services.AddSingleton<SchedulerService>();
        services.AddSingleton<ISchedulerService>(sp => sp.GetRequiredService<SchedulerService>());
        services.AddSingleton<IMediaIndexService>(sp => sp.GetRequiredService<MediaIndexService>());
        services.AddSingleton<IDatabaseSeedService, DatabaseSeedService>();
        services.AddSingleton<IScheduleMigrationService, ScheduleMigrationService>();

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
        services.AddSingleton<IBatteryOptimizationService, BatteryOptimizationService>();
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
        services.AddSingleton(_ => new MediaPlayer());
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
        services.AddSingleton(_ => TaskScheduler.Default);

#if WINDOWS
        // CRITICAL FIX FOR WINDOWS — INavigation is NOT auto-registered on Windows
        // This is a known MAUI bug that affects Windows but not Android/iOS
        // Android/iOS auto-register INavigation, but Windows does not
        services.AddSingleton(_ =>
        {
            var app = Application.Current;
            // Use Windows[0].Page instead of obsolete MainPage
            if (app?.Windows.Count > 0)
            {
                var window = app.Windows[0];
                if (window.Page is NavigationPage windowNavPage)
                {
                    return windowNavPage.Navigation;
                }
            }
            
            // Fallback — try MainPage for compatibility (obsolete but may be needed)
#pragma warning disable CS0618 // Type or member is obsolete
            if (app?.MainPage is NavigationPage navPage)
            {
                return navPage.Navigation;
            }
#pragma warning restore CS0618
            
            // During startup, this might not be ready yet
            throw new InvalidOperationException("INavigation is not available. NavigationPage must be set before resolving INavigation.");
        });
#endif
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
        services.AddTransient<MediaProgressViewModal>();

        // Register ScheduleListItem as transient for list items
        services.AddTransient<ScheduleListItem>();

        // Register factory for ScheduleListItem (takes AlarmSchedule and returns ScheduleListItem with DI)
        services.AddTransient<Func<AlarmSchedule, ScheduleListItem>>(serviceProvider =>
            schedule =>
            {
                var vm = serviceProvider.GetRequiredService<ScheduleListItem>();
                vm.Initialize(schedule);
                return vm;
            });
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
        services.AddTransient<LoadingPage>();
    }

    /// <summary>
    /// Initializes platform-specific bootstrap.
    /// This should be called after MauiApp is created to ensure databases and services are initialized.
    /// </summary>
    /// <param name="services">The service provider</param>
    /// <param name="isForeground">If true, runs bootstrap on a background Task. If false, runs synchronously.</param>
    public static void InitializePlatformBootstrap(IServiceProvider services, bool isForeground = false)
    {
        if (isForeground)
        {
            // Run bootstrap as a background job for foreground launches to avoid blocking UI
            Task.Run(() => RunBootstrap(services));
        }
        else
        {
            // Run bootstrap synchronously for background services/jobs
            RunBootstrap(services);
        }
    }

    private static void RunBootstrap(IServiceProvider services)
    {
        try
        {
            var logger = services.GetRequiredService<ILogger>();

#if ANDROID
            // Android bootstrap initialization
            var context = Platform.CurrentActivity?.ApplicationContext;
            var application = Platform.CurrentActivity?.Application;
            if (context != null && application != null)
            {
                BootstrapHelper.Initialize(logger, context, application);
            }
#elif IOS
            // iOS bootstrap initialization
            BootstrapHelper.Initialize(logger, isForeground: true);
#elif WINDOWS
            // Windows bootstrap initialization
            BootstrapHelper.Initialize(logger, isForeground: true);
#endif
        }
        catch (Exception ex)
        {
            // Log error but don't throw - bootstrap should not prevent app from running
            // Try to get logger from services if available, otherwise use static Log.Logger
            try
            {
                var logger = services.GetService<ILogger>();
                if (logger != null)
                {
                    logger.Error(ex, "Error in InitializePlatformBootstrap");
                }
                else
                {
                    Log.Logger.Error(ex, "Error in InitializePlatformBootstrap");
                }
            }
            catch
            {
                // If logging fails, silently continue - bootstrap should not prevent app from running
            }
        }
    }
}