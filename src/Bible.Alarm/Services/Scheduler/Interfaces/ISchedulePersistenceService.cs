namespace Bible.Alarm.Services.Scheduler.Interfaces;

public interface ISchedulePersistenceService : IDisposable
{
    Task<bool> SaveScheduleAsync(AlarmSchedule schedule, bool isNewSchedule, bool musicUpdated = true, bool biblePublicationUpdated = true);
    Task DeleteScheduleAsync(int scheduleId);
}

