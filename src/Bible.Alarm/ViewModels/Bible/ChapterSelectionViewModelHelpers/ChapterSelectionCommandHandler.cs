#nullable enable
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Bible.ChapterSelectionViewModelHelpers;

/// <summary>
/// Handles command execution for ChapterSelectionViewModel.
/// </summary>
public sealed class ChapterSelectionCommandHandler(
    ILogger logger,
    IState<ApplicationState> state,
    IDispatcher dispatcher,
    INavigationService navigationService)
{
    public async Task HandleSetChapterAsync(BibleChapterListViewItemModel chapter)
    {
        if (chapter == null)
        {
            return;
        }

        // Always use CurrentSchedule as the source of truth for language/publication codes
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null ||
            string.IsNullOrEmpty(currentSchedule.BibleReadingLanguageCode) ||
            string.IsNullOrEmpty(currentSchedule.BibleReadingPublicationCode) ||
            !currentSchedule.BibleReadingBookNumber.HasValue)
        {
            logger.Warning("ChapterSelectionViewModel: SetChapterCommand - CurrentSchedule is null or missing required properties");
            return;
        }

        // Map entity to DTO before dispatching
        var chapterSelectedItem = new BibleReadingStateItem
        {
            LanguageCode = currentSchedule.BibleReadingLanguageCode,
            PublicationCode = currentSchedule.BibleReadingPublicationCode,
            BookNumber = currentSchedule.BibleReadingBookNumber.Value,
            ChapterNumber = chapter.Number,
            // Store display names from current state
            LanguageName = currentSchedule.BibleReadingLanguageName,
            PublicationName = currentSchedule.BibleReadingPublicationName,
            BookName = currentSchedule.BibleReadingBookName
        };


        dispatcher.Dispatch(new ChapterSelectedAction(chapterSelectedItem));

        // Navigate back to schedule page
        await navigationService.PopModalAsync();
    }
}

