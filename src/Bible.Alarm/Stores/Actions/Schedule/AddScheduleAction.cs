using Fluxor;
using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Stores.Actions.Schedule;

public class AddScheduleAction
{
    public AlarmSchedule Schedule { get; }

    public AddScheduleAction(AlarmSchedule schedule)
    {
        Schedule = schedule;
    }
}