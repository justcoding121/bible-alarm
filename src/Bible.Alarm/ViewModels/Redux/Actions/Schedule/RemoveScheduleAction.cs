using Bible.Alarm.Common.Redux;

namespace Bible.Alarm.ViewModels.Redux.Actions.Schedule;

public class RemoveScheduleAction : IAction
{
    public ScheduleListItem ScheduleListItem { get; set; }
}