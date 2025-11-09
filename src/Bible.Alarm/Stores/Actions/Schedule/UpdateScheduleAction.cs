using Fluxor;
using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Stores.Actions.Schedule;

public class UpdateScheduleAction
{
    public AlarmSchedule Schedule { get; }

    public UpdateScheduleAction(AlarmSchedule schedule)
    {
        Schedule = schedule;
    }
}