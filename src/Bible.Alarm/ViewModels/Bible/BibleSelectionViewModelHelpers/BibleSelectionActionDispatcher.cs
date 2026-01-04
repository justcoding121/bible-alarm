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
        Log.Debug("BibleSelectionActionDispatcher: Dispatching ChapterSelectedAction - LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}, BookNumber: {BookNumber}, ChapterNumber: {ChapterNumber}",
            bibleReadingItem.LanguageCode, bibleReadingItem.PublicationCode, bibleReadingItem.BookNumber, bibleReadingItem.ChapterNumber);
        dispatcher.Dispatch(new ChapterSelectedAction(bibleReadingItem));
        
        Log.Debug("BibleSelectionActionDispatcher: Dispatching BibleSelectionAction");
        dispatcher.Dispatch(new BibleSelectionAction(bibleReadingItem));
        Log.Debug("BibleSelectionActionDispatcher: Both actions dispatched successfully");
    }

    public void DispatchLanguageSelectionActions(BibleReadingStateItem bibleReadingItem, LanguageListViewItemModel language)
    {
        Log.Information("BibleSelectionActionDispatcher: SelectLanguageCommand - Selected language: {LanguageName} ({LanguageCode}), Translation: {PublicationCode}, Book: {BookNumber}, Chapter: {ChapterNumber}",
            language.Name, language.Code, bibleReadingItem.PublicationCode, bibleReadingItem.BookNumber, bibleReadingItem.ChapterNumber);

        Log.Debug("BibleSelectionActionDispatcher: SelectLanguageCommand - Dispatching ChapterSelectedAction with LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}, BookNumber: {BookNumber}, ChapterNumber: {ChapterNumber}",
            bibleReadingItem.LanguageCode, bibleReadingItem.PublicationCode, bibleReadingItem.BookNumber, bibleReadingItem.ChapterNumber);
        dispatcher.Dispatch(new ChapterSelectedAction(bibleReadingItem));

        Log.Debug("BibleSelectionActionDispatcher: SelectLanguageCommand - Dispatching BibleSelectionAction");
        dispatcher.Dispatch(new BibleSelectionAction(bibleReadingItem));
    }
}
