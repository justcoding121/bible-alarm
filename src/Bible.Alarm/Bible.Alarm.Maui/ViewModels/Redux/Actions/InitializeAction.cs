using Bible.Alarm.Common.DataStructures;
using Redux;

namespace Bible.Alarm.ViewModels.Redux.Actions
{
    public class InitializeAction : IAction
    {
        public ObservableHashSet<ScheduleListItem> ScheduleList =
            new ObservableHashSet<ScheduleListItem>();
    }
}
