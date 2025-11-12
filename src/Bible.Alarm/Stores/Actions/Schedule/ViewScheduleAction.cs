using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Stores.Actions.Schedule;

public class ViewScheduleAction(AlarmSchedule selectedSchedule)
{
    public AlarmSchedule SelectedSchedule { get; } = selectedSchedule;
}