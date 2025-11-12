using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Stores.Actions.Bible;

public class BookSelectionAction(BibleReadingSchedule tentativeBibleReadingSchedule)
{
    public BibleReadingSchedule TentativeBibleReadingSchedule { get; } = tentativeBibleReadingSchedule;
}