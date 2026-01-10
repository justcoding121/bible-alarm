using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IBibleNavigationService
{
    Task<bool> MoveToPreviousSectionAsync(BibleReadingSchedule schedule);
    Task<bool> MoveToNextSectionAsync(BibleReadingSchedule schedule);
    Task<bool> MoveToPreviousChapterAsync(BibleReadingSchedule schedule);
    Task<bool> MoveToNextChapterAsync(BibleReadingSchedule schedule);
}

