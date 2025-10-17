using Bible.Alarm.Services;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.ViewModels.Shared;

namespace Bible.Alarm.ViewModels
{
    public static class IocSetup
    {
        public static void Initialize(IContainer container, bool isService)
        {
            if (!isService)
            {
                //marking as singleton because this is the starting point
                //and it has calls to Messenger.Subscribe
                container.RegisterSingleton((x) => 
                new HomeViewModel(container,
                    container.Resolve<ScheduleDbContext>(),
                    container.Resolve<IToastService>(),
                    container.Resolve<INavigationService>(),
                    container.Resolve<IMediaCacheService>(),
                    container.Resolve<IAlarmService>(),
                    container.Resolve<INotificationService>(),
                    container.Resolve<MediaDbContext>()));

                container.Register((x) => new ScheduleViewModel(container));

                container.Register((x) => new MusicSelectionViewModel(container));
                container.Register((x) => new SongBookSelectionViewModel(container));
                container.Register((x) => new TrackSelectionViewModel(container));

                container.Register((x) => new BibleSelectionViewModel(container));
                container.Register((x) => new BookSelectionViewModel(container));
                container.Register((x) => new ChapterSelectionViewModel(container));

                container.Register((x) => new AlarmViewModal(container));

                //marked as singleton due to call to Messenger.Subscribe
                container.RegisterSingleton((x) => new MediaProgressViewModal(container));
            }
        }

    }
}