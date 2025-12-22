#if IOS
using Bible.Alarm.Platforms.iOS.Services.Storage;
using Bible.Alarm.Platforms.iOS.Services.UI;
using Bible.Alarm.Platforms.iOS.Services.Handlers;
using Bible.Alarm.Platforms.iOS.Services.Handlers.Interfaces;
using Bible.Alarm.Platforms.iOS.Services.Platform;
using Bible.Alarm.Platforms.iOS.Services.Media;
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
#if WINDOWS
using Bible.Alarm.Platforms.Windows.Services.UI;
using Bible.Alarm.Platforms.Windows.Services.Handlers;
using Bible.Alarm.Platforms.Windows.Services.Handlers.Interfaces;
using Bible.Alarm.Platforms.Windows.Services.Media;
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
        services.AddAutoMapper(typeof(ServiceRegistrationHelper).Assembly);

        // Register Fluxor
        services.AddFluxor(options => options.ScanAssemblies(typeof(ServiceRegistrationHelper).Assembly));

        // Register logging
        services.AddSingleton(_ => Log.Logger);

        // Register core services that don't have platform dependencies
        services.AddSingleton<IDownloadService, DownloadService>();
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
        // MediaSessionCallback is created lazily by MediaSessionManager to avoid startup dependency issues
        services.AddSingleton<MediaSessionManager>();
        services.AddTransient(sp =>
            new ModernMediaSession(
                sp.GetRequiredService<MediaSessionManager>()));
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
        services.AddSingleton<INotificationService, WindowsNotificationService>();
        services.AddSingleton<IToastService, WindowsToastService>();
        services.AddSingleton<IStorageService, WindowsStorageService>();
        services.AddSingleton<IWindowsAlarmHandler, WindowsAlarmHandler>();
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
        services.AddTransient<AlarmViewModal>();

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
        services.AddTransient<BibleLanguageModal>();
        services.AddTransient<MusicLanguageModal>();
        services.AddTransient<AlarmModal>();
        services.AddTransient<BatteryOptimizationExclusionModal>();
        services.AddTransient<NumberOfChaptersModal>();
        services.AddTransient<BootstrapPage>();


        // It will be created with BootstrapPage as the root page
        services.AddTransient(sp =>
        {
            var bootstrapPage = sp.GetRequiredService<BootstrapPage>();
            var navigationPage = new NavigationPage(bootstrapPage);
            // NavigationPage background will adapt to theme via BootstrapPage
            NavigationPage.SetHasNavigationBar(bootstrapPage, false);
            return navigationPage;
        });
    }
}

