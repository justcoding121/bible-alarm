#nullable enable
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
        dispatcher.Dispatch(new TrackSelectedAction(biblePublicationItem));
        dispatcher.Dispatch(new BiblePublicationSelectionAction(biblePublicationItem));
    }

    public void DispatchLanguageSelectionActions(BiblePublicationStateItem biblePublicationItem, LanguageListViewItemModel language)
    {
        dispatcher.Dispatch(new TrackSelectedAction(biblePublicationItem));
        dispatcher.Dispatch(new BiblePublicationSelectionAction(biblePublicationItem));
    }
}
