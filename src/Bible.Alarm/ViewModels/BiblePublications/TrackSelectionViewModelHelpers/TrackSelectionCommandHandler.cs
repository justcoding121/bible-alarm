#nullable enable
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.BiblePublications;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.BiblePublications.TrackSelectionViewModelHelpers;

/// <summary>
/// Handles command execution for TrackSelectionViewModel.
/// </summary>
public sealed class TrackSelectionCommandHandler(
    ILogger logger,
    IState<ApplicationState> state,
    IDispatcher dispatcher,
    INavigationService navigationService)
{
    public async Task HandleSetTrackAsync(BiblePublicationTrackListViewItemModel track)
    {
        if (track == null)
        {
            return;
        }

        // Always use CurrentSchedule as the source of truth for language/publication codes
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null ||
            string.IsNullOrEmpty(currentSchedule.BiblePublicationLanguageCode) ||
            string.IsNullOrEmpty(currentSchedule.BiblePublicationCode) ||
            !currentSchedule.BiblePublicationSectionNumber.HasValue)
        {
            logger.Warning("TrackSelectionViewModel: SetTrackCommand - CurrentSchedule is null or missing required properties");
            return;
        }

        // Map entity to DTO before dispatching
        var trackSelectedItem = new BiblePublicationStateItem
        {
            LanguageCode = currentSchedule.BiblePublicationLanguageCode,
            PublicationCode = currentSchedule.BiblePublicationCode,
            SectionNumber = currentSchedule.BiblePublicationSectionNumber.Value,
            TrackNumber = track.Number,
            // Store display names from current state
            LanguageName = currentSchedule.BiblePublicationLanguageName,
            PublicationName = currentSchedule.BiblePublicationName,
            SectionName = currentSchedule.BiblePublicationSectionName
        };


        dispatcher.Dispatch(new TrackSelectedAction(trackSelectedItem));

        // Navigate back to schedule page
        await navigationService.PopModalAsync();
    }
}

