namespace Bible.Alarm.Services.iOS
{
    using Bible.Alarm.Contracts.Network;
    using Bible.Alarm.Contracts.Platform;
    using Bible.Alarm.Droid.Services.Storage;
    using Bible.Alarm.iOS.Services.Handlers;
    using Bible.Alarm.iOS.Services.Platform;
    using Bible.Alarm.Services.Contracts;
    using Microsoft.EntityFrameworkCore;
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Threading.Tasks;

    public static class IocSetup
    {

        public static void Initialize(IContainer container, bool isService)
        {
            container.Register<HttpMessageHandler>((x) => new NSUrlSessionHandler());

            if (!isService)
            {
                container.Register<IToastService>((x) => new IOsToastService(container.Resolve<TaskScheduler>()));
            }

            container.Register<INotificationService>((x) => new IOsNotificationService(container));

            container.Register<IPreviewPlayService>((x) => new PreviewPlayService(container, container.Resolve<IDownloadService>()));

            container.Register((x) =>
            {
                var storageService = container.Resolve<IStorageService>();
                string databasePath = storageService.StorageRoot;

                var scheduleDbConfig = new DbContextOptionsBuilder<ScheduleDbContext>()
                    .UseSqlite($"Filename={Path.Combine(databasePath, "bibleAlarm.db")}").Options;
                return new ScheduleDbContext(scheduleDbConfig);
            });


            container.Register((x) =>
            {
                var storageService = container.Resolve<IStorageService>();
                string databasePath = storageService.StorageRoot;

                var mediaDbConfig = new DbContextOptionsBuilder<MediaDbContext>()
                    .UseSqlite($"Filename={Path.Combine(databasePath, "mediaIndex.db")}").Options;
                return new MediaDbContext(mediaDbConfig);
            });


            container.Register<IVersionFinder>((x) => new VersionFinder());
            container.Register<IStorageService>((x) => new IOsStorageService());
            container.Register((x) =>
                    new IOsAlarmHandler(container.Resolve<IPlaybackService>(),
                                container.Resolve<TaskScheduler>()));
        }
    }
}