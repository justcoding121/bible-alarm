using Fluxor;
using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Stores.Actions.Bible;

public class ChapterSelectionAction
{
    public BibleReadingSchedule TentativeBibleReadingSchedule { get; }

    public ChapterSelectionAction(BibleReadingSchedule tentativeBibleReadingSchedule)
    {
        TentativeBibleReadingSchedule = tentativeBibleReadingSchedule;
    }
}