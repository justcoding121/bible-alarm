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
            string.IsNullOrEmpty(currentSchedule.BiblePublicationCode))
        {
            logger.Warning("TrackSelectionViewModel: SetTrackCommand - CurrentSchedule is null or missing required properties");
            return;
        }

        // For non-sectioned publications (dramas/videos), section number is 0 or null
        var effectiveSectionNumber = currentSchedule.BiblePublicationSectionNumber ?? 0;

        logger.Debug("TrackSelectionCommandHandler: Setting track {TrackNumber} ({TrackTitle}) for section {SectionNumber}",
            track.Number, track.Title, effectiveSectionNumber);

        // Map entity to DTO before dispatching
        var trackSelectedItem = new BiblePublicationStateItem
        {
            LanguageCode = currentSchedule.BiblePublicationLanguageCode,
            PublicationCode = currentSchedule.BiblePublicationCode,
            SectionNumber = effectiveSectionNumber,
            TrackNumber = track.Number,
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

