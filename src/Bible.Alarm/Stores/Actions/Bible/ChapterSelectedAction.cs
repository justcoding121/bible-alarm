using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Stores.Actions.Bible;

public class TrackSelectedAction(BibleReadingStateItem currentBibleReadingSchedule)
{
    public BibleReadingStateItem CurrentBibleReadingSchedule { get; } = currentBibleReadingSchedule;
}
