using Redux;

namespace Bible.Alarm.ViewModels.Redux.Actions
{
    public class ViewScheduleAction : IAction
    {
        public ScheduleListItem SelectedScheduleListItem { get; set; }
    }
}
