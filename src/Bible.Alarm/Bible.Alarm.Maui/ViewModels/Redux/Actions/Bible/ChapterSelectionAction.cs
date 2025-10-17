using Bible.Alarm.Models;
using Redux;

namespace Bible.Alarm.ViewModels.Redux.Actions.Bible
{
    public class ChapterSelectionAction : IAction
    {
        public BibleReadingSchedule TentativeBibleReadingSchedule { get; set; }
    }
}
