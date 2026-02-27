using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Stores.Actions.Schedule;

/// <summary>
/// Draft-only action: updates CurrentSchedule without touching the Schedules collection.
/// Used during browsing (before Save) to populate display names on the schedule page
/// without leaking unsaved changes to the home page or triggering side effects
/// like SetCarPlayScreenAction/preferences writes.
/// </summary>
public class UpdateDraftScheduleAction(ScheduleStateItem schedule)
{
    public ScheduleStateItem Schedule { get; } = schedule;
}
