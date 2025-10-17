using Redux;

namespace Bible.Alarm.ViewModels.Redux.Actions
{
    public class UpdateScheduleAction : IAction
    {
        public ScheduleListItem ScheduleListItem { get; set; }
    }
}
