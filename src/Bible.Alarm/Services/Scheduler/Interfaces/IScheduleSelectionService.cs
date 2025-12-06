using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Services.Scheduler.Interfaces;

public interface IScheduleSelectionService : IDisposable
{
    Task<AlarmMusic> LoadMusicForSelectionAsync(int scheduleId, bool isNewSchedule, bool musicUpdated, AlarmMusic currentMusic);
    Task<BibleReadingSchedule> LoadBibleReadingForSelectionAsync(int scheduleId, bool isNewSchedule, bool bibleReadingUpdated, BibleReadingSchedule currentBibleReading);
}

