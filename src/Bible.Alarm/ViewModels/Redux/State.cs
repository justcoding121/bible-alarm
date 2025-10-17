using Bible.Alarm.Common.DataStructures;
using Bible.Alarm.Models;

namespace Bible.Alarm.ViewModels.Redux
{
    public class ApplicationState
    {
        public ObservableHashSet<ScheduleListItem> Schedules { get; set; }

        public ScheduleListItem CurrentScheduleListItem { get; set; }

        public AlarmMusic CurrentMusic { get; set; }
        public AlarmMusic TentativeMusic { get; set; }

        public BibleReadingSchedule CurrentBibleReadingSchedule { get; set; }
        public BibleReadingSchedule TentativeBibleReadingSchedule { get; set; }
    }
}
