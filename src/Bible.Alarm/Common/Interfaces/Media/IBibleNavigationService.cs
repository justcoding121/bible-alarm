using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Common.Interfaces.Media;

public interface IBibleNavigationService
{
    Task<bool> MoveToPreviousBookAsync(BibleReadingSchedule schedule);
    Task<bool> MoveToNextBookAsync(BibleReadingSchedule schedule);
    Task<bool> MoveToPreviousChapterAsync(BibleReadingSchedule schedule);
    Task<bool> MoveToNextChapterAsync(BibleReadingSchedule schedule);
}

