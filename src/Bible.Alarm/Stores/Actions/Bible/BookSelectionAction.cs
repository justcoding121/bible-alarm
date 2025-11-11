using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Stores.Actions.Bible;

public class BookSelectionAction
{
    public BibleReadingSchedule TentativeBibleReadingSchedule { get; }

    public BookSelectionAction(BibleReadingSchedule tentativeBibleReadingSchedule)
    {
        TentativeBibleReadingSchedule = tentativeBibleReadingSchedule;
    }
}