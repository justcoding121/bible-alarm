using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Models.Schedule;

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

    public void Delete(int scheduleId)
    {
        RemoveNotification(scheduleId);
    }

    private void ScheduleNotification(AlarmSchedule schedule)
    {
        notificationService.ScheduleNotificationAsync(schedule,
            string.IsNullOrEmpty(schedule.Name) ? "Bible Alarm" : schedule.Name,
            "Press to start listening now.");
    }

    private void RemoveNotification(int scheduleId)
    {
        notificationService.RemoveAsync(scheduleId);
    }

    public void Dispose()
    {
        // Note: notificationService is a singleton
        // and should not be disposed here as it is managed by the DI container
    }
}