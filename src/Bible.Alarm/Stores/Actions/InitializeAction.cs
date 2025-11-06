using Bible.Alarm.Common.DataStructures;
using Bible.Alarm.Common.Redux;
using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Stores.Actions;

public class InitializeAction : IAction
{
    public ObservableHashSet<AlarmSchedule> ScheduleList = [];
}