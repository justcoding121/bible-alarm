using Bible.Alarm.Common.Redux;
using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Stores.Actions.Schedule;

public class RemoveScheduleAction : IAction
{
    public AlarmSchedule Schedule { get; set; }
}