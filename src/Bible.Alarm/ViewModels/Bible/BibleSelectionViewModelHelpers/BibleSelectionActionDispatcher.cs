#nullable enable
using Bible;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Bible.BibleSelectionViewModelHelpers;

/// <summary>
/// Handles dispatching actions for bible selection operations.
/// </summary>
public sealed class BibleSelectionActionDispatcher
{
    private readonly IDispatcher dispatcher;

    public BibleSelectionActionDispatcher(IDispatcher dispatcher)
    {
        this.dispatcher = dispatcher;
    }

    public void DispatchBibleReadingSelectionActions(BibleReadingStateItem bibleReadingItem)
    {
        dispatcher.Dispatch(new ChapterSelectedAction(bibleReadingItem));
        dispatcher.Dispatch(new BibleSelectionAction(bibleReadingItem));
    }

    public void DispatchLanguageSelectionActions(BibleReadingStateItem bibleReadingItem, LanguageListViewItemModel language)
    {
        dispatcher.Dispatch(new ChapterSelectedAction(bibleReadingItem));
        dispatcher.Dispatch(new BibleSelectionAction(bibleReadingItem));
    }
}
