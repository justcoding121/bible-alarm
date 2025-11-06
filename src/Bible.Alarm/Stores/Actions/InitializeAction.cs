using Bible.Alarm.Common.Redux;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.DataStructures;

namespace Bible.Alarm.Stores.Actions;

public class InitializeAction : IAction
{
    public ObservableHashSet<AlarmSchedule> ScheduleList = [];
}