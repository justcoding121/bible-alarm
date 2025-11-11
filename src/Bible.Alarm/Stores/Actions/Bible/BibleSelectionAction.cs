using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Stores.Actions.Bible;

public class BibleSelectionAction
{
    public BibleReadingSchedule CurrentBibleReadingSchedule { get; }
    public BibleReadingSchedule TentativeBibleReadingSchedule { get; }

    public BibleSelectionAction(BibleReadingSchedule currentBibleReadingSchedule, BibleReadingSchedule tentativeBibleReadingSchedule)
    {
        CurrentBibleReadingSchedule = currentBibleReadingSchedule;
        TentativeBibleReadingSchedule = tentativeBibleReadingSchedule;
    }
}