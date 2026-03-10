using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Common.Interfaces.UI;

public interface INotificationService
{
    Task ShowNotificationAsync(int scheduleId);
    Task ScheduleNotificationAsync(AlarmSchedule alarmSchedule, string title, string body);
    Task RemoveAsync(int scheduleId);
    Task<bool> IsScheduledAsync(int scheduleId);

    /// <summary>
    /// Dismisses any displayed/delivered notification for this schedule (e.g. after user opens app and starts playback).
    /// </summary>
    Task ClearDeliveredNotificationAsync(int scheduleId);

    Task<bool> CanScheduleAsync();
}
