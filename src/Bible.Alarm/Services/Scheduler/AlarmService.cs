using Bible.Alarm.Contracts.Media;
using Bible.Alarm.Contracts.Scheduler;
using Bible.Alarm.Contracts.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Infrastructure.Schedule;

namespace Bible.Alarm.Services.Scheduler;

public class AlarmService(
    INotificationService notificationService)
    : IAlarmService
{
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
        // Note: notificationService is a singleton
        // and should not be disposed here as it is managed by the DI container
    }
}