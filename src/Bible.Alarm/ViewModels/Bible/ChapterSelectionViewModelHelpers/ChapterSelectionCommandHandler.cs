#nullable enable
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Bible.TrackSelectionViewModelHelpers;

/// <summary>
/// Handles command execution for TrackSelectionViewModel.
/// </summary>
public sealed class TrackSelectionCommandHandler(
    ILogger logger,
    IState<ApplicationState> state,
    IDispatcher dispatcher,
    INavigationService navigationService)
{
    public async Task HandleSetTrackAsync(BibleTrackListViewItemModel track)
    {
        if (track == null)
        {
            return;
        }

        // Always use CurrentSchedule as the source of truth for language/publication codes
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null ||
            string.IsNullOrEmpty(currentSchedule.BibleReadingLanguageCode) ||
            string.IsNullOrEmpty(currentSchedule.BibleReadingPublicationCode) ||
            !currentSchedule.BibleReadingSectionNumber.HasValue)
        {
            logger.Warning("TrackSelectionViewModel: SetTrackCommand - CurrentSchedule is null or missing required properties");
            return;
        }

        // Map entity to DTO before dispatching
        var trackSelectedItem = new BibleReadingStateItem
        {
            LanguageCode = currentSchedule.BibleReadingLanguageCode,
            PublicationCode = currentSchedule.BibleReadingPublicationCode,
            SectionNumber = currentSchedule.BibleReadingSectionNumber.Value,
            TrackNumber = track.Number,
            // Store display names from current state
            LanguageName = currentSchedule.BibleReadingLanguageName,
            PublicationName = currentSchedule.BibleReadingPublicationName,
            SectionName = currentSchedule.BibleReadingSectionName
        };


        dispatcher.Dispatch(new TrackSelectedAction(trackSelectedItem));

        // Navigate back to schedule page
        await navigationService.PopModalAsync();
    }
}

