using Bible.Alarm.Models;
using Redux;

namespace Bible.Alarm.ViewModels.Redux.Actions
{
    public class BibleSelectionAction : IAction
    {
        public BibleReadingSchedule CurrentBibleReadingSchedule { get; set; }
        public BibleReadingSchedule TentativeBibleReadingSchedule { get; set; }
    }
}
