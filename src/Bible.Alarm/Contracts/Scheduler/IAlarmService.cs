using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Contracts.Scheduler;

public interface IAlarmService : IDisposable
{
    Task Create(AlarmSchedule schedule);
    void Update(AlarmSchedule schedule);
    void Delete(long scheduleId);
}