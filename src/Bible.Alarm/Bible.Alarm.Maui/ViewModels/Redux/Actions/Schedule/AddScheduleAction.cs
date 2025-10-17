using Redux;

namespace Bible.Alarm.ViewModels.Redux.Actions
{
    public class AddScheduleAction : IAction
    {
        public ScheduleListItem ScheduleListItem { get; set; }
    }
}
