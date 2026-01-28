using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Stores.Actions.BiblePublications;

public class BiblePublicationSectionSelectionAction(BiblePublicationStateItem currentBiblePublicationSchedule)
{
    public BiblePublicationStateItem CurrentBiblePublicationSchedule { get; } = currentBiblePublicationSchedule;
}
