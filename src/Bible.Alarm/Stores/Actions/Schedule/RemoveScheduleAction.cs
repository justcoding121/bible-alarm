using Fluxor;
using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Stores.Actions.Schedule;

public class RemoveScheduleAction
{
    public AlarmSchedule Schedule { get; }

    public RemoveScheduleAction(AlarmSchedule schedule)
    {
        Schedule = schedule;
    }
}