using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Stores.Actions.Schedule;

/// <summary>
/// Action dispatched from UI/ViewModel when user wants to create a new schedule.
/// Contains the domain model (ScheduleStateItem DTO) from the store/view model.
/// Following Fluxor best practices: Actions contain domain models, not DB entities.
/// </summary>
public class CreateScheduleAction(ScheduleStateItem schedule, bool musicUpdated = true, bool biblePublicationUpdated = true)
{
    public ScheduleStateItem Schedule { get; } = schedule;
    public bool MusicUpdated { get; } = musicUpdated;
    public bool BiblePublicationUpdated { get; } = biblePublicationUpdated;
}

