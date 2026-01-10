using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IBibleNavigationService
{
    Task<bool> MoveToPreviousSectionAsync(BiblePublicationSchedule schedule);
    Task<bool> MoveToNextSectionAsync(BiblePublicationSchedule schedule);
    Task<bool> MoveToPreviousTrackAsync(BiblePublicationSchedule schedule);
    Task<bool> MoveToNextTrackAsync(BiblePublicationSchedule schedule);
}

