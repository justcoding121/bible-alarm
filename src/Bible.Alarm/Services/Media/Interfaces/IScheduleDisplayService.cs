using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IScheduleDisplayService : IDisposable
{
    Task<string> GetTrackDisplayNameAsync(int scheduleId, bool force = false);
    Task<string> GetTrackDisplayNameForBibleReadingAsync(int scheduleId, BibleReadingSchedule bibleReadingSchedule, bool force = false);
}

