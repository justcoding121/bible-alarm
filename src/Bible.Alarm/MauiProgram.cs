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

namespace Bible.Alarm;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
#pragma warning disable CA1416
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkitMediaElement()
#pragma warning restore CA1416
            .ConfigureFonts(fonts => { fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"); });

        // Register services
        RegisterServices(builder.Services);

        var app = builder.Build();

        // Initialize the global service provider for access from multiple entry points
        ServiceProviderManager.Initialize(app.Services);

        return app;
    }

    private static void RegisterServices(IServiceCollection services)
    {
        // Register core services
        services.AddSingleton<HttpMessageHandler, HttpClientHandler>();

        // Register common services
        RegisterCommonServices(services);

        // Register ViewModels
        RegisterViewModels(services);

        // Register UI components
        RegisterUIComponents(services);
    }


    private static void RegisterCommonServices(IServiceCollection services)
    {
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

        // Register platform-specific services
#if ANDROID
        services.AddSingleton<INotificationService, Bible.Alarm.Services.Droid.DroidNotificationService>();
        services.AddSingleton<IToastService, Bible.Alarm.Services.Droid.DroidToastService>();
#elif IOS
        services.AddSingleton<INotificationService, Bible.Alarm.Services.iOS.IOsNotificationService>();
        services.AddSingleton<IToastService, Bible.Alarm.Services.iOS.IOsToastService>();
#elif WINDOWS
        services.AddSingleton<INotificationService, Bible.Alarm.Services.Windows.UwpNotificationService>();
        services.AddSingleton<IToastService, Bible.Alarm.Services.Windows.UwpToastService>();
#endif

        // Register TaskScheduler for compatibility
        services.AddSingleton<TaskScheduler>(sp => TaskScheduler.FromCurrentSynchronizationContext());

        // Register NavigationService
        services.AddSingleton<INavigationService>(sp =>
        {
            // This will be set up properly in App.xaml.cs
            return new NavigationService(null);
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

    private static void RegisterUIComponents(IServiceCollection services)
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