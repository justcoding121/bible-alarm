using Bible.Alarm.Models;
using System;
using System.Threading.Tasks;

namespace Bible.Alarm.Services.Contracts
{
    public interface INotificationService : IDisposable
    {
        Task ShowNotification(long scheduleId);
        Task ScheduleNotification(AlarmSchedule alarmSchedule,  string title, string body);
        Task Remove(long scheduleId);
        Task<bool> IsScheduled(long scheduleId);

        Task<bool> CanSchedule();
    }
}
