#if IOS
using Bible.Alarm.Platforms.iOS.Effects;
using Bible.Alarm.Platforms.iOS.Services.Bootstrap;
using Bible.Alarm.Platforms.iOS.Services.Media;
using Bible.Alarm.Platforms.iOS.Services.Media.Interfaces;
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
using Bible.Alarm.Platforms.Android.Services.Audio.Interfaces;
using Bible.Alarm.Platforms.Android.Services.Battery;
using Bible.Alarm.Platforms.Android.Services.Bootstrap;
using Bible.Alarm.Platforms.Android.Services.Handlers;
using Bible.Alarm.Platforms.Android.Services.Media;
using Bible.Alarm.Platforms.Android.Services.Media.Interfaces;
using Bible.Alarm.Platforms.Android.Services.Platform;
using Bible.Alarm.Platforms.Android.Services.Storage;
using Bible.Alarm.Platforms.Android.Services.UI;
using Bible.Alarm.Services.Battery;
using Bible.Alarm.Services.Battery.Interfaces;
#endif

#if WINDOWS
using Bible.Alarm.Platforms.Windows.Services.Bootstrap;
#endif

using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Common.Interfaces.Platform;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Database;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Network;
using Bible.Alarm.Services.Network.Interfaces;
using Bible.Alarm.Shared.Helpers;
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
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers.Interfaces;
using Bible.Alarm.ViewModels.BiblePublications;
using Bible.Alarm.ViewModels.Categories;
#if WINDOWS
using Bible.Alarm.Platforms.Windows.Services.Media;
using Bible.Alarm.Platforms.Windows.Services.Media.Interfaces;
using Bible.Alarm.Platforms.Windows.Services.UI;
using Bible.Alarm.Platforms.Windows.Services.UI.Interfaces;
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
        
        // Register HttpClient for LanguageContentService
        services.AddSingleton<System.Net.Http.HttpClient>();

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

        // Register thread-safe Preferences service (must be singleton to share lock across all instances)
        services.AddSingleton<Bible.Alarm.Common.Interfaces.Storage.IThreadSafePreferencesService, Bible.Alarm.Common.Services.Storage.ThreadSafePreferencesService>();

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
        services.AddSingleton<ISchedulePersistenceService, SchedulePersistenceService>();
        services.AddSingleton<IDefaultScheduleService, DefaultScheduleService>();
        services.AddSingleton<IAlarmScheduleService, AlarmScheduleService>();
        services.AddSingleton<IGeneralSettingsService, GeneralSettingsService>();
        services.AddSingleton<IBiblePublicationScheduleService, BiblePublicationScheduleService>();
        services.AddSingleton<IBiblePublicationService, BiblePublicationService>();
        services.AddSingleton<IBiblePublicationSectionService, BiblePublicationSectionService>();
        services.AddSingleton<IBiblePublicationTrackService, BiblePublicationTrackService>();
        services.AddSingleton<ICategoryService, CategoryService>();
        services.AddSingleton<Bible.Alarm.Shared.Services.Media.Interfaces.ILanguageNameService, Bible.Alarm.Shared.Services.Media.LanguageNameService>();
        services.AddSingleton<Bible.Alarm.Shared.Services.Media.Interfaces.ICategoryNameService, Bible.Alarm.Shared.Services.Media.CategoryNameService>();
        services.AddSingleton<IUrlConstructionService>(sp => 
            new UrlConstructionService(sp.GetRequiredService<IServiceScopeFactory>()));
        services.AddSingleton<IMelodyMusicService, MelodyMusicService>();
        services.AddSingleton<IVocalMusicService, VocalMusicService>();
        services.AddSingleton<IInternetConnectivityChecker>(sp =>
            new Bible.Alarm.Services.Network.InternetConnectivityCheckerAdapter(sp.GetRequiredService<INetworkStatusService>()));
        services.AddSingleton<ILanguageContentService>(sp => new Bible.Alarm.Shared.Services.Media.LanguageContentService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ILogger>(),
            sp.GetRequiredService<System.Net.Http.HttpClient>(),
            sp.GetService<IInternetConnectivityChecker>()));
        services.AddSingleton<Bible.Alarm.Shared.Services.Media.Interfaces.IMelodyDiscTracksApiRefresher>(sp =>
            new Bible.Alarm.Shared.Services.Media.MelodyDiscTracksApiRefresher(
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<System.Net.Http.HttpClient>(),
                sp.GetRequiredService<ILogger>()));
        services.AddSingleton<ITrackCdnUrlRefresher, TrackCdnUrlRefresher>();
        services.AddSingleton<ICdnPlaybackUrlProbe>(sp =>
            new CdnPlaybackUrlProbe(sp.GetRequiredService<System.Net.Http.HttpClient>(), sp.GetRequiredService<ILogger>()));
        services.AddSingleton<IBiblePublicationNavigationService, BiblePublicationNavigationService>();
        services.AddSingleton<IMediaCacheSetupService, MediaCacheSetupService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IScheduleItemStateService, ScheduleItemStateService>();
        services.AddSingleton<IExceptionHandlingService, ExceptionHandlingService>();
        services.AddSingleton<IWindowSetupService, WindowSetupService>();
        services.AddSingleton<IReviewPromptService, ReviewPromptService>();
        services.AddSingleton<IAppLifecycleService, AppLifecycleService>();
        services.AddSingleton<IPlaybackModalService, PlaybackModalService>();
        services.AddSingleton<IMessageHandlingService, MessageHandlingService>();
        services.AddSingleton<IScheduleSelectionService, ScheduleSelectionService>();
        services.AddSingleton<INetworkStatusService, NetworkStatusService>();
        services.AddSingleton<IDisplayMetadataService, DisplayMetadataService>();
        services.AddSingleton<IMediaElementService, MediaElementService>();
        services.AddSingleton<IAudioPlayer, AudioPlayer>();
        services.AddSingleton<IPlaybackService, PlaybackService>();

#if ANDROID
        services.AddSingleton<IDefaultDeviceRingtoneService, Platforms.Android.Services.Media.AndroidDefaultDeviceRingtoneService>();
#elif IOS
        services.AddSingleton<IDefaultDeviceRingtoneService, Platforms.iOS.Services.Media.iOSDefaultDeviceRingtoneService>();
#elif WINDOWS
        services.AddSingleton<IDefaultDeviceRingtoneService, Platforms.Windows.Services.Media.WindowsDefaultDeviceRingtoneService>();
#else
        services.AddSingleton<IDefaultDeviceRingtoneService, DefaultDeviceRingtoneServiceNoOp>();
#endif

        // Register platform-specific accessibility font scale service (required by FontService)
#if ANDROID
        services.AddSingleton<IAccessibilityFontScaleService, AndroidAccessibilityFontScaleService>();
#elif IOS
        services.AddSingleton<IAccessibilityFontScaleService, iOSAccessibilityFontScaleService>();
#elif WINDOWS
        services.AddSingleton<IAccessibilityFontScaleService, WindowsAccessibilityFontScaleService>();
#endif

        services.AddSingleton<IFontService, FontService>();
        services.AddSingleton<ISchedulerService, SchedulerService>();
        services.AddSingleton<IDatabaseSeedService, DatabaseSeedService>();
        services.AddSingleton<IScheduleMigrationService, ScheduleMigrationService>();
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
        services.AddSingleton<IScheduleStateChangeHandler, ScheduleStateChangeHandler>();

        // Register bootstrap services
        services.AddSingleton<IDatabaseBootstrapService, DatabaseBootstrapService>();
        services.AddSingleton<IFluxorBootstrapService, FluxorBootstrapService>();
        services.AddSingleton<IResourceBootstrapService, ResourceBootstrapService>();
        services.AddSingleton<IScheduleBootstrapService, ScheduleBootstrapService>();
        services.AddSingleton<IBootstrapOrchestrator, BootstrapOrchestrator>();

        // Register platform-specific bootstrap service (notification channels, background jobs)
#if ANDROID
        services.AddSingleton<IPlatformBootstrapService, AndroidPlatformBootstrapService>();
#elif IOS
        services.AddSingleton<IPlatformBootstrapService, iOSPlatformBootstrapService>();
#elif WINDOWS
        services.AddSingleton<IPlatformBootstrapService, WindowsPlatformBootstrapService>();
#endif

        // Single version source from Bible.Alarm assembly (ApplicationDisplayVersion in csproj)
        services.AddSingleton<IVersionFinder, AssemblyAppVersionFinder>();

        // Register platform-specific services
#if ANDROID
        services.AddSingleton<INotificationService, AndroidNotificationService>();
        services.AddSingleton<IToastService, AndroidToastService>();
        services.AddSingleton<IBatteryOptimizationService, BatteryOptimizationService>();
        services.AddSingleton<IAndroidAlarmHandler, AndroidAlarmHandler>();
        services.AddSingleton<Bible.Alarm.Platforms.Android.Services.UI.Interfaces.IAndroidMiniPlaybackBarHost, Bible.Alarm.Platforms.Android.Services.UI.AndroidMiniPlaybackBarHost>();
        services.AddSingleton<IStorageService, AndroidStorageService>();
        services.AddSingleton<IBatteryOptimizationManager, AndroidBatteryOptimizationManager>();
        services.AddSingleton<IAndroidPlayerNotificationService, AndroidPlayerNotificationService>();
        services.AddSingleton<IAndroidArtworkService, AndroidArtworkService>();
        // Register global audio focus listener and service as singletons
        services.AddSingleton<IAudioFocusListener, AudioFocusListener>();
        services.AddSingleton<IAudioFocusService, AudioFocusService>();
        // Register MediaSessionCompat using the global helper (thread-safe, prevents duplicates)
        // This ensures MediaSession is created as early as possible, even in background processes
        services.AddSingleton(sp => Platforms.Android.Services.Media.MediaSessionHelper.Create());
        // MediaSessionCallback is created lazily by MediaSessionManager to avoid startup dependency issues
        services.AddSingleton<IMediaSessionManager, MediaSessionManager>();
        // Register MediaSession effect for Android Auto
        services.AddSingleton<MediaSessionEffect>();
        // Register global audio focus effect that manages audio focus based on playback state
        services.AddSingleton<AudioFocusEffect>();
        // Default schedule rotation when Android Auto connected and not playing (every 5 min)
        services.AddSingleton<Bible.Alarm.Platforms.Android.Services.AndroidAuto.Interfaces.IAndroidAutoDefaultScheduleRotationService, Bible.Alarm.Platforms.Android.Services.AndroidAuto.AndroidAutoDefaultScheduleRotationService>();
#elif IOS
        services.AddSingleton<INotificationService, IOsNotificationService>();
        services.AddSingleton<IToastService, IOsToastService>();
        services.AddSingleton<IStorageService, IOsStorageService>();
        services.AddSingleton<IIosAlarmHandler, IOsAlarmHandler>();
        // Register iOS Now Playing and Remote Command services for Lock Screen, Control Center, AirPods, and CarPlay
        services.AddSingleton<IiOSRemoteCommandCenterManager, iOSRemoteCommandCenterManager>();
        services.AddSingleton<IiOSNowPlayingInfoManager, iOSNowPlayingInfoManager>();
        // Register iOS MediaSession effect for syncing playback state with system media controls
        services.AddSingleton<iOSMediaSessionEffect>();
        // Default schedule rotation when CarPlay connected and not playing (every 5 min)
        services.AddSingleton<Bible.Alarm.Platforms.iOS.Services.CarPlay.Interfaces.ICarPlayDefaultScheduleRotationService, Bible.Alarm.Platforms.iOS.Services.CarPlay.CarPlayDefaultScheduleRotationService>();
#elif WINDOWS
        // Register WindowsNotificationService by interface; INotificationService resolves to same instance
        services.AddSingleton<IWindowsNotificationService, WindowsNotificationService>();
        services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<IWindowsNotificationService>());
        services.AddSingleton<IToastService, WindowsToastService>();
        services.AddSingleton<IStorageService, WindowsStorageService>();
        services.AddSingleton<IWindowsAlarmHandler, WindowsAlarmHandler>();
        // Register Windows media toast effect for rich playback notifications
        services.AddSingleton<Platforms.Windows.Effects.WindowsMediaToastEffect>();
        // Register Windows SMTC service for handling system media transport controls
        services.AddSingleton<IWindowsSmtcService, WindowsSmtcService>();
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
                b =>
                {
                    b.MigrationsAssembly("Bible.Alarm.Shared");
                    b.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                });
        });

        // Register TaskScheduler for compatibility - use default scheduler instead of UI context
        // TaskScheduler doesn't implement IDisposable, so use regular AddSingleton
        services.AddSingleton(_ => TaskScheduler.Default);
    }

    private static void RegisterViewModels(IServiceCollection services)
    {
        services.AddTransient<HomeViewModel>();
        services.AddTransient<ScheduleViewModel>();
        services.AddTransient<MusicPublicationSelectionViewModel>();
        services.AddTransient<ViewModels.Music.MusicTrackSelectionViewModel>();
        services.AddTransient<BiblePublicationSelectionViewModel>();
        services.AddTransient<CategorySelectionViewModel>();
        services.AddTransient<BiblePublicationSectionSelectionViewModel>();
        services.AddTransient<ViewModels.BiblePublications.BiblePublicationTrackSelectionViewModel>();
        services.AddTransient<ViewModels.Music.MusicSectionSelectionViewModel>();
        services.AddTransient<PlaybackViewModel>();
        services.AddSingleton<MiniPlaybackBarViewModel>();
        services.AddTransient<BiblePublicationSelectionContainerViewModel>();
        services.AddTransient<MusicSelectionContainerViewModel>();
        services.AddTransient<NumberOfTrackContainerViewModel>();
        services.AddTransient<ScheduleDetailsContainerViewModel>();
        services.AddTransient<AlarmSettingsContainerViewModel>();

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
        services.AddTransient<MusicPublicationSelection>();
        services.AddTransient<Views.Music.MusicTrackSelection>();
        services.AddTransient<BiblePublicationSelection>();
        services.AddTransient<BiblePublicationSectionSelection>();
        services.AddTransient<BiblePublicationTrackSelection>();
        services.AddTransient<BiblePublicationLanguageModal>();
        services.AddTransient<MusicLanguageModal>();
        services.AddTransient<CategorySelectionModal>();
        services.AddTransient<PlaybackModal>();
        services.AddTransient<BiblePublicationSelectionModal>();
        services.AddTransient<BiblePublicationSectionSelectionModal>();
        services.AddTransient<Views.Music.MusicTrackSelectionModal>();
        services.AddTransient<Views.Music.MusicSectionSelectionModal>();
        services.AddTransient<MusicPublicationSelectionModal>();
        services.AddTransient<BiblePublicationTrackSelectionModal>();
        services.AddTransient<AndroidAlarmPermissionModal>();
        services.AddTransient<NotificationPermissionModal>();
        services.AddTransient<NumberOfTracksModal>();
        services.AddTransient(sp =>
        {
            var homePage = sp.GetRequiredService<Home>();
            var navigationPage = new NavigationPage(homePage);
            NavigationPage.SetHasNavigationBar(homePage, false);
            NavigationPage.SetHasBackButton(homePage, false);
            return navigationPage;
        });
    }
}
