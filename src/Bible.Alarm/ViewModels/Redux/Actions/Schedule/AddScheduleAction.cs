using Bible.Alarm.Common.Redux;

namespace Bible.Alarm.ViewModels.Redux.Actions.Schedule;

public class AddScheduleAction : IAction
{
    public ScheduleListItem ScheduleListItem { get; set; }
}