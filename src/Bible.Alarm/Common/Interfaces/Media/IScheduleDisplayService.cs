using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Common.Interfaces.Media;

public interface IScheduleDisplayService
{
    Task<string> GetChapterDisplayNameAsync(int scheduleId, bool force = false);
    Task<string> GetChapterDisplayNameForBibleReadingAsync(int scheduleId, BibleReadingSchedule bibleReadingSchedule, bool force = false);
}

