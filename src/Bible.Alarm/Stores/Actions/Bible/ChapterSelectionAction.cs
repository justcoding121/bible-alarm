using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Stores.Actions.Bible;

public class ChapterSelectionAction(BibleReadingStateItem tentativeBibleReadingSchedule)
{
    public BibleReadingStateItem TentativeBibleReadingSchedule { get; } = tentativeBibleReadingSchedule;
}
