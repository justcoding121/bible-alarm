using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Stores.Actions.BiblePublications;

public class TrackSelectionAction(BiblePublicationStateItem currentBiblePublicationSchedule)
{
    public BiblePublicationStateItem CurrentBiblePublicationSchedule { get; } = currentBiblePublicationSchedule;
}
