using Bible.Alarm.Common.Redux;
using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Stores.Actions.Bible;

public class ChapterSelectionAction : IAction
{
    public BibleReadingSchedule TentativeBibleReadingSchedule { get; set; }
}