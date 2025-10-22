using Bible.Alarm.Common.Redux;

namespace Bible.Alarm.ViewModels.Redux.Actions.Schedule;

public class ViewScheduleAction : IAction
{
    public ScheduleListItem SelectedScheduleListItem { get; set; }
}