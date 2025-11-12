using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Common.Interfaces.Scheduler;

public interface ISchedulePersistenceService
{
    Task<bool> SaveScheduleAsync(AlarmSchedule schedule, bool isNewSchedule, bool musicUpdated = true, bool bibleReadingUpdated = true);
    Task DeleteScheduleAsync(int scheduleId);
    Task<AlarmSchedule> GetSampleScheduleAsync();
}

