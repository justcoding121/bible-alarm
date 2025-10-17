namespace Bible.Alarm.Services.Droid
{
    using Android.App;
    using Android.Media;
    using Bible.Alarm.Contracts.Battery;
    using Bible.Alarm.Contracts.Media;
    using Bible.Alarm.Contracts.Platform;
    using Bible.Alarm.Droid.Services.Battery;
    using Bible.Alarm.Droid.Services.Handlers;
    using Bible.Alarm.Droid.Services.Platform;
    using Bible.Alarm.Droid.Services.Storage;
    using Bible.Alarm.Services.Contracts;
    using Bible.Alarm.Services.Droid.Tasks;
    using MediaManager;
    using Microsoft.EntityFrameworkCore;
    using System;
    using System.IO;
    using System.Net.Http;
    using Xamarin.Android.Net;

    public static class IocSetup
    {
        private static Lazy<IMediaManager> mediaManagerImplementation
            = new Lazy<IMediaManager>(() =>
            {
                return new MediaManagerImplementation();
            }, System.Threading.LazyThreadSafetyMode.PublicationOnly);

        public static void Initialize(IContainer container, bool isService)
        {
            container.Register<HttpMessageHandler>((x) => new AndroidClientHandler());

            container.Register<IToastService>((x) => new DroidToastService(container));

            DroidNotificationService notificationServiceFactory() => new DroidNotificationService(container, container.Resolve<IStorageService>());

            container.Register<DroidNotificationService>((x) => notificationServiceFactory());
            container.Register<INotificationService>((x) => notificationServiceFactory());

            container.Register<IPreviewPlayService>((x) => new PreviewPlayService(container, container.Resolve<MediaPlayer>()));
            container.Register((x) =>
            {
                var player = new MediaPlayer();
                return player;
            });

            container.Register<IStorageService>((x) => new AndroidStorageService());

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

            container.RegisterSingleton((x) =>
            {
                CrossMediaManager.Implementation = mediaManagerImplementation;
                return CrossMediaManager.Current;
            });

            container.Register<IBatteryOptimizationManager>((x) => new BatteryOptimizationManager(container));
            container.Register<IVersionFinder>((x) => new VersionFinder());

            AndroidAlarmHandler alarmHandlerFactory() => new AndroidAlarmHandler(container.Resolve<IMediaManager>(),
                                                        container.Resolve<IPlaybackService>(), container.Resolve<ScheduleDbContext>(),
                                                        container.Resolve<DroidNotificationService>());

            container.Register<AndroidAlarmHandler>((x) => alarmHandlerFactory());
            container.Register<IAndroidAlarmHandler>((x) => alarmHandlerFactory());
        }

    }
}