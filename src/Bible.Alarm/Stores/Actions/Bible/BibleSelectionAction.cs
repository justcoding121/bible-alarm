using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Stores.Actions.Bible;

public class BibleSelectionAction(
    BibleReadingStateItem currentBibleReadingSchedule,
    BibleReadingStateItem tentativeBibleReadingSchedule)
{
    public BibleReadingStateItem CurrentBibleReadingSchedule { get; } = currentBibleReadingSchedule;
    public BibleReadingStateItem TentativeBibleReadingSchedule { get; } = tentativeBibleReadingSchedule;
}