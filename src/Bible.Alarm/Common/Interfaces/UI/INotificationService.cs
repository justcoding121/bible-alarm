using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Common.Interfaces.UI;

public interface INotificationService : IDisposable
{
    Task ShowNotification(int scheduleId);
    Task ScheduleNotification(AlarmSchedule alarmSchedule, string title, string body);
    Task Remove(int scheduleId);
    Task<bool> IsScheduled(int scheduleId);

    Task<bool> CanSchedule();
}