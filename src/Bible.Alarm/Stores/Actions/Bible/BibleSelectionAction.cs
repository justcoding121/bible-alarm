using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Stores.Actions.Bible;

public class BibleSelectionAction(
    BibleReadingSchedule currentBibleReadingSchedule,
    BibleReadingSchedule tentativeBibleReadingSchedule)
{
    public BibleReadingSchedule CurrentBibleReadingSchedule { get; } = currentBibleReadingSchedule;
    public BibleReadingSchedule TentativeBibleReadingSchedule { get; } = tentativeBibleReadingSchedule;
}