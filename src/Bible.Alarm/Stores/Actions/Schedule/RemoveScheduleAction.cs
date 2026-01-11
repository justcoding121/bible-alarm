using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Stores.Actions.Schedule;

public class RemoveScheduleAction(AlarmSchedule schedule)
{
    public AlarmSchedule Schedule { get; } = schedule;
}
