using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Stores.Actions.Schedule;

public class AddScheduleAction(AlarmSchedule schedule)
{
    public AlarmSchedule Schedule { get; } = schedule;
}
