#nullable enable

using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Stores.Actions.Schedule;

public class ViewScheduleAction(ScheduleStateItem? selectedSchedule)
{
    public ScheduleStateItem? SelectedSchedule { get; } = selectedSchedule;
}
