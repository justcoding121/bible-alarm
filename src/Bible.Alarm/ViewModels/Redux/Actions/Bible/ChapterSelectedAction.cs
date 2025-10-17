using Bible.Alarm.Models;
using Redux;

namespace Bible.Alarm.ViewModels.Redux.Actions.Bible
{
    public class ChapterSelectedAction : IAction
    {
        public BibleReadingSchedule CurrentBibleReadingSchedule { get; set; }
    }
}
