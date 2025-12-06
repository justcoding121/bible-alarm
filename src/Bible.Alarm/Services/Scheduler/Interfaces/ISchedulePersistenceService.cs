using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Services.Scheduler.Interfaces;

public interface ISchedulePersistenceService : IDisposable
{
    Task<bool> SaveScheduleAsync(AlarmSchedule schedule, bool isNewSchedule, bool musicUpdated = true, bool bibleReadingUpdated = true);
    Task DeleteScheduleAsync(int scheduleId);
    Task<AlarmSchedule> GetSampleScheduleAsync();
}

