namespace Bible.Alarm.Services.Windows
{
    using Bible.Alarm;
    using Bible.Alarm.Contracts.Network;
    using Bible.Alarm.Contracts.Platform;
    using Bible.Alarm.Services;
    using Bible.Alarm.Services.Contracts;
    using Bible.Alarm.Services.Windows.Platform;
    using Bible.Alarm.Services.Windows.Storage;
    using Bible.Alarm.Services.Windows.Handlers;
    using Microsoft.EntityFrameworkCore;
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Threading.Tasks;

    public static class IocSetup
    {
        public static IContainer Container { get; private set; }


        public static void Initialize(IContainer container, bool isService)
        {
            Container = container;

            container.Register<HttpMessageHandler>((x) => new HttpClientHandler());

            container.Register<IToastService>((x) => new UwpToastService(container.Resolve<TaskScheduler>()));

            container.Register<INotificationService>((x) => new UwpNotificationService(container));

            container.Register<global::Windows.Media.Playback.MediaPlayer>((x) => new global::Windows.Media.Playback.MediaPlayer());
            container.Register<IPreviewPlayService>((x) => new Bible.Alarm.Services.Windows.PreviewPlayService(container.Resolve<global::Windows.Media.Playback.MediaPlayer>()));

            container.Register((x) =>
            {
                var storageService = container.Resolve<IStorageService>();
                string databasePath = storageService.StorageRoot;

                var scheduleDbConfig = new DbContextOptionsBuilder<ScheduleDbContext>()
                    .UseSqlite($"Data Source={Path.Combine(databasePath, "bibleAlarm.db")}").Options;
                return new ScheduleDbContext(scheduleDbConfig);
            });

            container.Register((x) =>
            {
                var storageService = container.Resolve<IStorageService>();
                string databasePath = storageService.StorageRoot;

                var mediaDbConfig = new DbContextOptionsBuilder<MediaDbContext>()
                    .UseSqlite($"Data Source={Path.Combine(databasePath, "mediaIndex.db")}").Options;
                return new MediaDbContext(mediaDbConfig);
            });


            container.Register<IVersionFinder>((x) => new UwpVersionFinder());
            container.Register<IStorageService>((x) => new UwpStorageService());
            container.Register((x) =>
                    new UwpAlarmHandler(container.Resolve<IPlaybackService>()));
        }
    }
}