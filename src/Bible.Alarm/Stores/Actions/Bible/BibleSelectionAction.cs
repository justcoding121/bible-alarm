using Bible.Alarm.Common.Redux;
using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Stores.Actions.Bible;

public class BibleSelectionAction : IAction
{
    public BibleReadingSchedule CurrentBibleReadingSchedule { get; set; }
    public BibleReadingSchedule TentativeBibleReadingSchedule { get; set; }
}