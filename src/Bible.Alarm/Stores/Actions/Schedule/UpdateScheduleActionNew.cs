using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Stores.Actions.Schedule;

/// <summary>
/// Action dispatched from UI/ViewModel when user wants to update an existing schedule.
/// Contains the domain model (ScheduleStateItem DTO) from the store/view model.
/// Following Fluxor best practices: Actions contain domain models, not DB entities.
/// 
/// Note: This is the new pattern. The old UpdateScheduleAction (taking AlarmSchedule) is kept
/// for backward compatibility with services that already have DB entities.
/// </summary>
public class UpdateScheduleFromViewModelAction(ScheduleStateItem schedule, bool musicUpdated = true, bool bibleReadingUpdated = true)
{
    public ScheduleStateItem Schedule { get; } = schedule;
    public bool MusicUpdated { get; } = musicUpdated;
    public bool BibleReadingUpdated { get; } = bibleReadingUpdated;
}

