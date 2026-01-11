using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Services.Scheduler.Interfaces;

public interface IAlarmService
{
    Task Create(AlarmSchedule schedule);
    Task Update(AlarmSchedule schedule);
    Task Delete(int scheduleId);
}

