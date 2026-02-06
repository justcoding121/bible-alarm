#nullable enable
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.BiblePublications;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.BiblePublications.BiblePublicationTrackSelectionViewModelHelpers;

/// <summary>
/// Handles command execution for BiblePublicationTrackSelectionViewModel.
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
            string.IsNullOrEmpty(currentSchedule.BiblePublicationCode))
        {
            logger.Warning("BiblePublicationTrackSelectionViewModel: SetTrackCommand - CurrentSchedule is null or missing required properties");
            return;
        }

        var sectionCode = currentSchedule.BiblePublicationSectionCode;

        logger.Debug("TrackSelectionCommandHandler: Setting track {TrackCode} ({TrackTitle}) for section {SectionCode}",
            track.Number, track.Title, sectionCode ?? "(none)");

        // Map entity to DTO before dispatching
        var trackSelectedItem = new BiblePublicationStateItem
        {
            LanguageCode = currentSchedule.BiblePublicationLanguageCode,
            PublicationCode = currentSchedule.BiblePublicationCode,
            SectionCode = sectionCode,
            TrackCode = TrackCodeHelper.GetFromTrack(track.Track),
            // Store display names from current state and list item
            LanguageName = currentSchedule.BiblePublicationLanguageName,
            LanguageDirection = currentSchedule.BiblePublicationLanguageDirection,
            PublicationName = currentSchedule.BiblePublicationName,
            SectionName = currentSchedule.BiblePublicationSectionName,
            TrackTitle = track.Title
        };

        dispatcher.Dispatch(new TrackSelectedAction(trackSelectedItem));

        // Navigate back to schedule page
        await navigationService.PopModalAsync();
    }
}

