using Bible.Alarm.Models;
using Bible.Alarm.Services.Contracts;
using System.Threading.Tasks;

namespace Bible.Alarm.Services
{
    public class AlarmService : IAlarmService
    {
        private readonly IContainer container;
        private INotificationService notificationService;
        private IMediaCacheService mediaCacheService;
        private ScheduleDbContext scheduleDbContext;

        public AlarmService(IContainer container,
            INotificationService notificationService,
            IMediaCacheService mediaCacheService,
            ScheduleDbContext scheduleDbContext)
        {
            this.container = container;
            this.notificationService = notificationService;
            this.mediaCacheService = mediaCacheService;
            this.scheduleDbContext = scheduleDbContext;
        }

        public Task Create(AlarmSchedule schedule)
        {
            scheduleNotification(schedule);
            return Task.CompletedTask;
        }

        public void Update(AlarmSchedule schedule)
        {
            removeNotification(schedule.Id);

            if (schedule.IsEnabled)
            {
                scheduleNotification(schedule);
            }
        }

        public void Delete(long scheduleId)
        {
            removeNotification(scheduleId);
        }

        private void scheduleNotification(AlarmSchedule schedule)
        {
            notificationService.ScheduleNotification(schedule, string.IsNullOrEmpty(schedule.Name) ? "Bible Alarm" : schedule.Name,
                "Press to start listening now.");
        }

        private void removeNotification(long scheduleId)
        {
            notificationService.Remove(scheduleId);
        }

        public void Dispose()
        {
            scheduleDbContext.Dispose();
            notificationService.Dispose();
            mediaCacheService.Dispose();
        }
    }
}
