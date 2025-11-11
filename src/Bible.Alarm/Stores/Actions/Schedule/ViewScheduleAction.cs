using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Stores.Actions.Schedule;

public class ViewScheduleAction
{
    public AlarmSchedule SelectedSchedule { get; }

    public ViewScheduleAction(AlarmSchedule selectedSchedule)
    {
        SelectedSchedule = selectedSchedule;
    }
}