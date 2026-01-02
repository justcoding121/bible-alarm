#nullable enable
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Music.TrackSelectionViewModelHelpers;

/// <summary>
/// Handles track selection logic for TrackSelectionViewModel.
/// </summary>
public sealed class TrackSelectionHandler(
    ILogger logger,
    IDispatcher dispatcher,
    IState<ApplicationState> state,
    IMapper mapper,
    INavigationService navigationService)
{
    public async Task HandleTrackSelection(MusicTrackListViewItemModel track, AlarmMusic? current)
    {
        if (track == null)
        {
            return;
        }

        // Ensure current is set from state if it's null
        if (current == null)
        {
            var stateValue = state.Value;
            if (stateValue.CurrentMusic != null)
            {
                current = mapper.Map<AlarmMusic>(stateValue.CurrentMusic);
            }
        }

        // Always use CurrentSchedule as the source of truth for music type/language/publication codes
        // This ensures we use the latest state, not stale data from 'current' field
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null ||
            !currentSchedule.MusicType.HasValue ||
            string.IsNullOrEmpty(currentSchedule.MusicPublicationCode))
        {
            logger.Warning("TrackSelectionHandler: HandleTrackSelection - CurrentSchedule is null or missing required properties");
            return;
        }

        // Update current for tracking purposes
        if (current == null)
        {
            current = new AlarmMusic();
        }
        current.TrackNumber = track.Number;
        current.Repeat = track.Repeat;

        // Map entity to DTO before dispatching
        // IMPORTANT: Include display names from list items (no database query needed)
        // Get music type/language/publication codes and display names from CurrentSchedule (they should already be populated)
        var trackSelectedItem = new MusicStateItem
        {
            MusicType = currentSchedule.MusicType.Value,
            LanguageCode = currentSchedule.MusicLanguageCode,
            PublicationCode = currentSchedule.MusicPublicationCode,
            TrackNumber = track.Number,
            Repeat = track.Repeat,
            // Store display names from list items and current state
            LanguageName = currentSchedule.MusicLanguageName,
            PublicationName = currentSchedule.MusicPublicationName,
            TrackName = track.Title
        };

        logger.Information("TrackSelectionHandler: HandleTrackSelection - Dispatching TrackSelectedAction. MusicType: {MusicType}, LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}, TrackNumber: {TrackNumber}",
            trackSelectedItem.MusicType, trackSelectedItem.LanguageCode ?? "null", trackSelectedItem.PublicationCode, trackSelectedItem.TrackNumber);

        dispatcher.Dispatch(new TrackSelectedAction(trackSelectedItem));

        // Navigate back to schedule page
        await navigationService.PopModalAsync();
    }
}
