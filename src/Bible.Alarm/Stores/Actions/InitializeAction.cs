using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.DataStructures;

namespace Bible.Alarm.Stores.Actions;

public class InitializeAction(ObservableHashSet<AlarmSchedule> scheduleList)
{
    public ObservableHashSet<AlarmSchedule> ScheduleList { get; } = scheduleList ?? [];
}