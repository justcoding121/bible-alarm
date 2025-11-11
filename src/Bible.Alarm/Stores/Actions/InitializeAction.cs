using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.DataStructures;

namespace Bible.Alarm.Stores.Actions;

public class InitializeAction
{
    public ObservableHashSet<AlarmSchedule> ScheduleList { get; }

    public InitializeAction(ObservableHashSet<AlarmSchedule> scheduleList)
    {
        ScheduleList = scheduleList ?? new ObservableHashSet<AlarmSchedule>();
    }
}