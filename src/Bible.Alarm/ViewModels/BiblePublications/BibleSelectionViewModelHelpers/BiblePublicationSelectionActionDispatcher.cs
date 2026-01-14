#nullable enable
using Bible.Alarm.Stores.Actions.BiblePublications;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;

/// <summary>
/// Handles dispatching actions for bible selection operations.
/// </summary>
public sealed class BiblePublicationSelectionActionDispatcher
{
    private readonly IDispatcher dispatcher;

    public BiblePublicationSelectionActionDispatcher(IDispatcher dispatcher)
    {
        this.dispatcher = dispatcher;
    }

    public void DispatchBiblePublicationSelectionActions(BiblePublicationStateItem biblePublicationItem)
    {
        // Only dispatch TrackSelectedAction - its reducer updates both CurrentSchedule and
        // CurrentBiblePublicationSchedule, and its effect syncs all properties.
        // Previously this also dispatched BiblePublicationSelectionAction which caused
        // redundant state updates leading to state cycles in container view models.
        dispatcher.Dispatch(new TrackSelectedAction(biblePublicationItem));
    }

    public void DispatchLanguageSelectionActions(BiblePublicationStateItem biblePublicationItem, LanguageListViewItemModel language)
    {
        // Only dispatch TrackSelectedAction - its reducer updates both CurrentSchedule and
        // CurrentBiblePublicationSchedule, and its effect syncs all properties.
        // Previously this also dispatched BiblePublicationSelectionAction which caused
        // redundant state updates leading to state cycles in container view models.
        dispatcher.Dispatch(new TrackSelectedAction(biblePublicationItem));
    }
}
