using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Common.Interfaces.Scheduler;

public interface IAlarmService : IDisposable
{
    Task Create(AlarmSchedule schedule);
    void Update(AlarmSchedule schedule);
    void Delete(int scheduleId);
}