using Bible.Alarm.Common.Redux;

namespace Bible.Alarm.ViewModels.Redux.Actions.Schedule;

public class UpdateScheduleAction : IAction
{
    public ScheduleListItem ScheduleListItem { get; set; }
}