using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Stores.Actions.Bible;

public class ChapterSelectedAction(BibleReadingSchedule currentBibleReadingSchedule)
{
    public BibleReadingSchedule CurrentBibleReadingSchedule { get; } = currentBibleReadingSchedule;
}