using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Common.Interfaces.Media;

public interface IScheduleDisplayService
{
    Task<string> GetChapterDisplayNameAsync(long scheduleId, bool force = false);
    Task<string> GetChapterDisplayNameForBibleReadingAsync(long scheduleId, BibleReadingSchedule bibleReadingSchedule, bool force = false);
}

