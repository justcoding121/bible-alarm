using Bible.Alarm.Models;
using Redux;

namespace Bible.Alarm.ViewModels.Redux.Actions.Bible
{
    public class BookSelectionAction : IAction
    {
        public BibleReadingSchedule TentativeBibleReadingSchedule { get; set; }
    }
}
