#if IOS
using Bible.Alarm.Platforms.iOS.Services.Storage;
using Bible.Alarm.Platforms.iOS.Services.UI;
using Bible.Alarm.Platforms.iOS.Services.Handlers;
using Bible.Alarm.Platforms.iOS.Services.Handlers.Interfaces;
using Bible.Alarm.Platforms.iOS.Services.Platform;
#endif

#if ANDROID
using Bible.Alarm.Common.Interfaces.Battery;
using Bible.Alarm.Platforms.Android.Effects;
using Bible.Alarm.Platforms.Android.Services.AndroidAuto;
using Bible.Alarm.Platforms.Android.Services.Audio;
using Bible.Alarm.Platforms.Android.Services.Battery;
using Bible.Alarm.Platforms.Android.Services.Handlers;
using Bible.Alarm.Platforms.Android.Services.Media;
using Bible.Alarm.Platforms.Android.Services.Platform;
using Bible.Alarm.Platforms.Android.Services.Storage;
using Bible.Alarm.Platforms.Android.Services.UI;
using Bible.Alarm.Services.Battery;
using Bible.Alarm.Services.Battery.Interfaces;
#endif

using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Common.Interfaces.Platform;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Database;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Network;
using Bible.Alarm.Services.Network.Interfaces;
using Bible.Alarm.Services.Scheduler;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.Schedule;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Services.Bootstrap;
using Bible.Alarm.Services.Bootstrap.Interfaces;
using Bible.Alarm.Services.Storage;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Services.UI;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Services.Media;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.ViewModels;
using Bible.Alarm.ViewModels.Bible;
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.ViewModels.Schedule;
using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.Views;
using Bible.Alarm.Views.Bible;
using Bible.Alarm.Views.General;
using Bible.Alarm.Views.Music;
using Bible.Alarm.Views.Schedule;
using Bible.Alarm.Views.Shared;
using Fluxor;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;
#if WINDOWS
using Bible.Alarm.Platforms.Windows.Services.UI;
using Bible.Alarm.Platforms.Windows.Services.Handlers;
using Bible.Alarm.Platforms.Windows.Services.Handlers.Interfaces;
using Bible.Alarm.Platforms.Windows.Services.Storage;
using Bible.Alarm.Platforms.Windows.Services.Platform;
#endif

namespace Bible.Alarm.Common.Helpers;

public static class ServiceRegistrationHelper
{
    /// <summary>
    /// Registers all services for the application
    /// </summary>
    public static void RegisterServices(IServiceCollection services)
    {
        // Register HttpMessageHandler (same implementation for all platforms)
        services.AddSingleton<HttpMessageHandler, HttpClientHandler>();

        // Register common services
        RegisterCommonServices(services);

        // Register ViewModels
        RegisterViewModels(services);

        // Register UI components
        RegisterUiComponents(services);
    }

    private static void RegisterCommonServices(IServiceCollection services)
    {
        // Register AutoMapper
        services.AddAutoMapper(cfg => cfg.AddMaps(typeof(ServiceRegistrationHelper).Assembly));

        // Register Fluxor
        services.AddFluxor(options =>
        {
            options.ScanAssemblies(typeof(ServiceRegistrationHelper).Assembly);
#if DEBUG
            // Enable Redux DevTools for debugging (optional, can help diagnose Effect registration issues)
            // options.UseReduxDevTools();
#endif
        });
        
        // Explicitly register ScheduleEffects to ensure it's available for Effect discovery
        // Note: Fluxor should auto-discover Effects, but explicit registration ensures DI can resolve it
        services.AddScoped<Bible.Alarm.Stores.Effects.ScheduleEffects>();

        // Register logging
        services.AddSingleton(_ => Log.Logger);

        // Register core services that don't have platform dependencies
        services.AddSingleton<IDownloadService, DownloadService>();
        services.AddSingleton<IMediaIndexVersionService, MediaIndexVersionService>();
        services.AddSingleton<IMediaIndexService, MediaIndexService>();
        services.AddSingleton<IMediaService, MediaService>();
        services.AddSingleton<IMediaCacheService, MediaCacheService>();
        services.AddSingleton<IMediaUrlRefreshService, MediaUrlRefreshService>();
        services.AddSingleton<IPlaylistService, PlaylistService>();
        services.AddSingleton<IPreparePlaybackService, PreparePlaybackService>();
        services.AddSingleton<IFallbackAlarmSoundService, FallbackAlarmSoundService>();
        services.AddSingleton<IAlarmService, AlarmService>();
        services.AddSingleton<IScheduleStateService, ScheduleStateService>();
        services.AddSingleton<ISchedulePlaybackService, SchedulePlaybackService>();
        services.AddSingleton<IScheduleDisplayService, ScheduleDisplayService>();
        services.AddSingleton<ISchedulePersistenceService, SchedulePersistenceService>();
        services.AddSingleton<IDefaultScheduleService, DefaultScheduleService>();
        services.AddSingleton<IAlarmScheduleService, AlarmScheduleService>();
        services.AddSingleton<IGeneralSettingsService, GeneralSettingsService>();
        services.AddSingleton<IAlarmMusicService, AlarmMusicService>();
        services.AddSingleton<IBibleReadingScheduleService, BibleReadingScheduleService>();
        services.AddSingleton<IBibleTranslationService, BibleTranslationService>();
        services.AddSingleton<IBibleBookService, BibleBookService>();
        services.AddSingleton<IBibleChapterService, BibleChapterService>();
        services.AddSingleton<IMelodyMusicService, MelodyMusicService>();
        services.AddSingleton<IVocalMusicService, VocalMusicService>();
        services.AddSingleton<IBibleNavigationService, BibleNavigationService>();
        services.AddSingleton<IMediaCacheSetupService, MediaCacheSetupService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IScheduleItemStateService, ScheduleItemStateService>();
        services.AddSingleton<IExceptionHandlingService, ExceptionHandlingService>();
        services.AddSingleton<IWindowSetupService, WindowSetupService>();
        services.AddSingleton<IAppLifecycleService, AppLifecycleService>();
        services.AddSingleton<IAlarmModalService, AlarmModalService>();
        services.AddSingleton<IMessageHandlingService, MessageHandlingService>();
        services.AddSingleton<IScheduleSelectionService, ScheduleSelectionService>();
        services.AddSingleton<INetworkStatusService, NetworkStatusService>();
        services.AddSingleton<IDisplayMetadataService, DisplayMetadataService>();
        services.AddSingleton<IMediaElementService, MediaElementService>();
        services.AddSingleton<IAudioPlayer, AudioPlayer>();
        services.AddSingleton<IPlaybackService, PlaybackService>();
        services.AddSingleton<IFontService, FontService>();
        services.AddSingleton<ISchedulerService, SchedulerService>();
        services.AddSingleton<IDatabaseSeedService, DatabaseSeedService>();
        services.AddSingleton<IScheduleMigrationService, ScheduleMigrationService>();
        services.AddSingleton<IMediaMigrationService, MediaMigrationService>();
        services.AddSingleton<IScheduleDatabaseVersionService, ScheduleDatabaseVersionService>();
        services.AddSingleton<IDiskCacheService, DiskCacheService>();

        // Register schedule services
        services.AddSingleton<IScheduleDisplayNameService, ScheduleDisplayNameService>();
        services.AddSingleton<IScheduleSaveService, ScheduleSaveService>();
        services.AddSingleton<IScheduleValidationService, ScheduleValidationService>();
        services.AddSingleton<IScheduleInitializationService, ScheduleInitializationService>();
        services.AddSingleton<IScheduleCommandService, ScheduleCommandService>();
        services.AddSingleton<IScheduleMediaCacheService, ScheduleMediaCacheService>();
        services.AddSingleton<IScheduleContainerService, ScheduleContainerService>();
        services.AddSingleton<ScheduleStateChangeHandler>();

        // Register bootstrap services
        services.AddSingleton<IDatabaseBootstrapService, DatabaseBootstrapService>();
        services.AddSingleton<IFluxorBootstrapService, FluxorBootstrapService>();
        services.AddSingleton<IResourceBootstrapService, ResourceBootstrapService>();
        services.AddSingleton<IScheduleBootstrapService, ScheduleBootstrapService>();
        services.AddSingleton<IBootstrapOrchestrator, BootstrapOrchestrator>();

        // Register platform-specific version finder
#if ANDROID
        services.AddSingleton<IVersionFinder, AndroidVersionFinder>();
#elif IOS
        services.AddSingleton<IVersionFinder, IOsVersionFinder>();
#elif WINDOWS
        services.AddSingleton<IVersionFinder, WindowsVersionFinder>();
#endif

        // Register platform-specific services
#if ANDROID
        services.AddSingleton<INotificationService, AndroidNotificationService>();
        services.AddSingleton<IToastService, AndroidToastService>();
        services.AddSingleton<IBatteryOptimizationService, BatteryOptimizationService>();
        services.AddSingleton<IAndroidAlarmHandler, AndroidAlarmHandler>();
        services.AddSingleton<IStorageService, AndroidStorageService>();
        services.AddSingleton<IBatteryOptimizationManager, AndroidBatteryOptimizationManager>();
        services.AddSingleton<IAndroidPlayerNotificationService, AndroidPlayerNotificationService>();
        services.AddSingleton<AndroidArtworkService>();
        // Register global audio focus listener and service as singletons
        services.AddSingleton<AudioFocusListener>();
        services.AddSingleton<AudioFocusService>();
        // Register MediaSessionCompat using the global helper (thread-safe, prevents duplicates)
        // This ensures MediaSession is created as early as possible, even in background processes
        services.AddSingleton(sp => Platforms.Android.Services.Media.MediaSessionHelper.Create());
        // MediaSessionCallback is created lazily by MediaSessionManager to avoid startup dependency issues
        services.AddSingleton<Platforms.Android.Services.Media.MediaSessionManager>();
        services.AddTransient(sp =>
            new ModernMediaSession(
                sp.GetRequiredService<Platforms.Android.Services.Media.MediaSessionManager>()));
        // Register MediaSession effect for Android Auto
        services.AddSingleton<MediaSessionEffect>();
        // Register global audio focus effect that manages audio focus based on playback state
        services.AddSingleton<AudioFocusEffect>();
#elif IOS
        services.AddSingleton<INotificationService, IOsNotificationService>();
        services.AddSingleton<IToastService, IOsToastService>();
        services.AddSingleton<IStorageService, IOsStorageService>();
        services.AddSingleton<IIosAlarmHandler, IOsAlarmHandler>();
#elif WINDOWS
        // Register WindowsNotificationService as both interface and concrete type
        // (concrete type needed for WindowsMediaToastEffect which uses ShowMediaToast method)
        services.AddSingleton<WindowsNotificationService>();
        services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<WindowsNotificationService>());
        services.AddSingleton<IToastService, WindowsToastService>();
        services.AddSingleton<IStorageService, WindowsStorageService>();
        services.AddSingleton<IWindowsAlarmHandler, WindowsAlarmHandler>();
        // Register Windows media toast effect for rich playback notifications
        services.AddSingleton<Platforms.Windows.Effects.WindowsMediaToastEffect>();
        // Register Windows SMTC service for handling system media transport controls
        services.AddSingleton<Platforms.Windows.Services.Media.WindowsSmtcService>();
        // Register Windows SMTC effect for initializing and updating SMTC
        services.AddSingleton<Platforms.Windows.Effects.WindowsSmtcEffect>();
#endif

        // Register database contexts
        services.AddDbContext<ScheduleDbContext>((sp, options) =>
        {
            var storageService = sp.GetRequiredService<IStorageService>();
            var databasePath = Path.Combine(storageService.StorageRoot, AppConstants.Database.ScheduleDatabaseFileName);
            options.UseSqlite(
                string.Format(AppConstants.Database.ScheduleDatabaseConnectionStringFormat, databasePath),
                b => b.MigrationsAssembly("Bible.Alarm.Shared"));
        });

        services.AddDbContext<MediaDbContext>((sp, options) =>
        {
            var storageService = sp.GetRequiredService<IStorageService>();
            var databasePath = Path.Combine(storageService.StorageRoot, AppConstants.Database.MediaIndexDatabaseFileName);
            options.UseSqlite(
                string.Format(AppConstants.Database.MediaIndexDatabaseConnectionStringFormat, databasePath),
                b => b.MigrationsAssembly("Bible.Alarm.Shared"));
        });

        // Register TaskScheduler for compatibility - use default scheduler instead of UI context
        // TaskScheduler doesn't implement IDisposable, so use regular AddSingleton
        services.AddSingleton(_ => TaskScheduler.Default);
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
        services.AddTransient<AlarmViewModel>();
        services.AddTransient<BibleSelectionContainerViewModel>();
        services.AddTransient<MusicSelectionContainerViewModel>();
        services.AddTransient<NumberOfChapterContainerViewModel>();
        services.AddTransient<ScheduleDetailsContainerViewModel>();

        // Register ScheduleListItem as transient for list items
        services.AddTransient<ScheduleListItemViewModel>();

        // Register factory for ScheduleListItem (takes schedule ID and returns ScheduleListItem with DI)
        // ScheduleListItem initializes from state using the schedule ID
        services.AddTransient<Func<int, ScheduleListItemViewModel>>(serviceProvider =>
            scheduleId =>
            {
                var vm = serviceProvider.GetRequiredService<ScheduleListItemViewModel>();
                vm.SetScheduleId(scheduleId);
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
        services.AddTransient<BibleLanguageModal>();
        services.AddTransient<MusicLanguageModal>();
        services.AddTransient<AlarmModal>();
        services.AddTransient<BibleSelectionModal>();
        services.AddTransient<BookSelectionModal>();
        services.AddTransient<ChapterSelectionModal>();
        services.AddTransient<MusicSelectionModal>();
        services.AddTransient<SongBookSelectionModal>();
        services.AddTransient<TrackSelectionModal>();
        services.AddTransient<AlarmSettingsModal>();
        services.AddTransient<NumberOfChaptersModal>();

        // It will be created with Home as the root page
        services.AddTransient(sp =>
        {
            var homePage = sp.GetRequiredService<Home>();
            var navigationPage = new NavigationPage(homePage);
            // NavigationPage background will adapt to theme via Home
            NavigationPage.SetHasNavigationBar(homePage, false);
            NavigationPage.SetHasBackButton(homePage, false);
            return navigationPage;
        });
    }
}

