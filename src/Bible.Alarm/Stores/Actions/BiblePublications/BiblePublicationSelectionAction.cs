using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Stores.Actions.BiblePublications;

public class BiblePublicationSelectionAction(BiblePublicationStateItem currentBiblePublicationSchedule)
{
    public BiblePublicationStateItem CurrentBiblePublicationSchedule { get; } = currentBiblePublicationSchedule;
}
