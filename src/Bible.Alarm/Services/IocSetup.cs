using Microsoft.Maui.ApplicationModel;
namespace Bible.Alarm.Services
{
    using Bible.Alarm.Contracts.Network;
    using Bible.Alarm.Contracts.Platform;
    using Bible.Alarm.Services.Contracts;
    using Bible.Alarm.Services.Network;
    using Bible.Alarm.Services.Tasks;
    using Bible.Alarm.Services.Media;
    using System;
    using System.Net.Http;

    public static class IocSetup
    {
        private static object @lock = new object();
        private static IPlaybackService playbackService;

        public static void Initialize(IContainer container, bool isService)
        {
            container.Register<IDownloadService>((x) => new DownloadService(container.Resolve<HttpMessageHandler>()));
            container.Register((x) => new MediaIndexService(container.Resolve<IStorageService>(), container.Resolve<IVersionFinder>(), container.Resolve<IDownloadService>()));
            container.Register((x) => new MediaService(container.Resolve<MediaIndexService>(), container.Resolve<MediaDbContext>()));

            container.Register<IMediaCacheService>((x) =>
                new MediaCacheService(container.Resolve<IStorageService>(),
                container.Resolve<IDownloadService>(),
                container.Resolve<IPlaylistService>(),
                container.Resolve<ScheduleDbContext>(),
                container.Resolve<MediaService>(),
                container.Resolve<INetworkStatusService>(),
                container.Resolve<IPlaybackService>()));

            container.Register<IPlaylistService>((x) => new PlaylistService(container.Resolve<ScheduleDbContext>(),
                container.Resolve<MediaDbContext>(),
                container.Resolve<MediaService>()));

            container.Register<IAlarmService>((x) => new AlarmService(container,
              container.Resolve<INotificationService>(),
              container.Resolve<IMediaCacheService>(),
              container.Resolve<ScheduleDbContext>()));

            container.Register<INetworkStatusService>((x) => new NetworkStatusService(container));
            
            // Register MediaElement-based audio service
            container.RegisterSingleton<IMediaElementAudioService>((x) => new MediaElementAudioService());

            Func<IPlaybackService> playbackServiceFactory = new Func<IPlaybackService>(() =>
            {
                lock (@lock)
                {

                    if (playbackService == null)
                    {
                        playbackService = new PlaybackService(container.Resolve<IMediaElementAudioService>());
                    }

                    return playbackService;
                }
            });

            container.RegisterSingleton<IPlaybackService>((x) => playbackServiceFactory());

            container.Register((x) => new SchedulerTask(container.Resolve<ScheduleDbContext>(),
                                 container.Resolve<IMediaCacheService>(), container.Resolve<IAlarmService>(),
                                 container.Resolve<INotificationService>(),
                                 container.Resolve<IStorageService>()));
        }

    }
}