using Bible.Alarm.Models;

namespace Bible.Alarm.Services.Contracts;

public interface IAlarmService : IDisposable
{
    Task Create(AlarmSchedule schedule);
    void Update(AlarmSchedule schedule);
    void Delete(long scheduleId);
}