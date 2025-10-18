using Bible.Alarm.Models;
using Bible.Alarm.Services.Contracts;

namespace Bible.Alarm.Services;

public class AlarmService(
    IContainer container,
    INotificationService notificationService,
    IMediaCacheService mediaCacheService,
    ScheduleDbContext scheduleDbContext)
    : IAlarmService
{
    private readonly IContainer _container = container;

    public Task Create(AlarmSchedule schedule)
    {
        ScheduleNotification(schedule);
        return Task.CompletedTask;
    }

    public void Update(AlarmSchedule schedule)
    {
        RemoveNotification(schedule.Id);

        if (schedule.IsEnabled) ScheduleNotification(schedule);
    }

    public void Delete(long scheduleId)
    {
        RemoveNotification(scheduleId);
    }

    private void ScheduleNotification(AlarmSchedule schedule)
    {
        notificationService.ScheduleNotification(schedule,
            string.IsNullOrEmpty(schedule.Name) ? "Bible Alarm" : schedule.Name,
            "Press to start listening now.");
    }

    private void RemoveNotification(long scheduleId)
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