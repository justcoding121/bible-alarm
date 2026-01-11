using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IScheduleDisplayService : IDisposable
{
    Task<string> GetTrackDisplayNameAsync(int scheduleId, bool force = false);
    Task<string> GetTrackDisplayNameForBiblePublicationAsync(int scheduleId, BiblePublicationSchedule biblePublicationSchedule, bool force = false);
}

