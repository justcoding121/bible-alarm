using Fluxor;
using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Stores.Actions.Bible;

public class ChapterSelectedAction
{
    public BibleReadingSchedule CurrentBibleReadingSchedule { get; }

    public ChapterSelectedAction(BibleReadingSchedule currentBibleReadingSchedule)
    {
        CurrentBibleReadingSchedule = currentBibleReadingSchedule;
    }
}