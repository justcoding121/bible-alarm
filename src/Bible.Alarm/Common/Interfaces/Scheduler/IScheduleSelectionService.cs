using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Common.Interfaces.Scheduler;

public interface IScheduleSelectionService
{
    Task<AlarmMusic> LoadMusicForSelectionAsync(long scheduleId, bool isNewSchedule, bool musicUpdated, AlarmMusic currentMusic);
    Task<BibleReadingSchedule> LoadBibleReadingForSelectionAsync(long scheduleId, bool isNewSchedule, bool bibleReadingUpdated, BibleReadingSchedule currentBibleReading);
}

