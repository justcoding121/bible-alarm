using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Stores.Actions;

public class InitializeAction(ObservableHashSet<ScheduleStateItem> scheduleList)
{
    public ObservableHashSet<ScheduleStateItem> ScheduleList { get; } = scheduleList ?? [];
}