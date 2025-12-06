using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IScheduleDisplayService : IDisposable
{
    Task<string> GetChapterDisplayNameAsync(int scheduleId, bool force = false);
    Task<string> GetChapterDisplayNameForBibleReadingAsync(int scheduleId, BibleReadingSchedule bibleReadingSchedule, bool force = false);
}

