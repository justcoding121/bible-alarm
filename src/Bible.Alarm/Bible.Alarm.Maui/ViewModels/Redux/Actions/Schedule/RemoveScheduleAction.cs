using Redux;

namespace Bible.Alarm.ViewModels.Redux.Actions
{
    public class RemoveScheduleAction : IAction
    {
        public ScheduleListItem ScheduleListItem { get; set; }
    }
}
