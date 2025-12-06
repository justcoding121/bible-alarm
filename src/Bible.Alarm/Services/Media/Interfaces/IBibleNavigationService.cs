using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IBibleNavigationService : IDisposable
{
    Task<bool> MoveToPreviousBookAsync(BibleReadingSchedule schedule);
    Task<bool> MoveToNextBookAsync(BibleReadingSchedule schedule);
    Task<bool> MoveToPreviousChapterAsync(BibleReadingSchedule schedule);
    Task<bool> MoveToNextChapterAsync(BibleReadingSchedule schedule);
}

