using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Common.Interfaces.UI;

public interface INotificationService
{
    Task ShowNotificationAsync(int scheduleId);
    Task ScheduleNotificationAsync(AlarmSchedule alarmSchedule, string title, string body);
    Task RemoveAsync(int scheduleId);
    Task<bool> IsScheduledAsync(int scheduleId);

    Task<bool> CanScheduleAsync();
}
