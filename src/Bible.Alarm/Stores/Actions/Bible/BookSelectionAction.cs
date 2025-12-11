using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Stores.Actions.Bible;

public class BookSelectionAction(BibleReadingStateItem tentativeBibleReadingSchedule)
{
    public BibleReadingStateItem TentativeBibleReadingSchedule { get; } = tentativeBibleReadingSchedule;
}