using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Contracts.UI;

public interface INotificationService : IDisposable
{
    Task ShowNotification(long scheduleId);
    Task ScheduleNotification(AlarmSchedule alarmSchedule, string title, string body);
    Task Remove(long scheduleId);
    Task<bool> IsScheduled(long scheduleId);

    Task<bool> CanSchedule();
}