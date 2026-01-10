using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Stores.Actions.Bible;

public class BiblePublicationSelectionAction(BibleReadingStateItem currentBibleReadingSchedule)
{
    public BibleReadingStateItem CurrentBibleReadingSchedule { get; } = currentBibleReadingSchedule;
}
