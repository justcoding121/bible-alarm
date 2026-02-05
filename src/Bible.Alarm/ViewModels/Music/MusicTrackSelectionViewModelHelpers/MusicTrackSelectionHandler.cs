#nullable enable
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Models;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.ViewModels.Music.MusicTrackSelectionViewModelHelpers;

/// <summary>
/// Handles track selection logic for MusicTrackSelectionViewModel.
/// </summary>
public sealed class MusicTrackSelectionHandler(
    IDispatcher dispatcher,
    IState<ApplicationState> state,
    INavigationService navigationService)
{
    public async Task HandleTrackSelection(MusicTrackListViewItemModel track, AlarmMusic? current)
    {
        if (track == null)
            return;

        if (current == null)
        {
            var stateValue = state.Value;
            if (stateValue.CurrentSchedule != null && !string.IsNullOrEmpty(stateValue.CurrentSchedule.MusicPublicationCode))
            {
                var schedule = stateValue.CurrentSchedule;
                current = new AlarmMusic
                {
                    LanguageCode = schedule.MusicLanguageCode,
                    PublicationCode = schedule.MusicPublicationCode ?? string.Empty,
                    TrackNumber = schedule.MusicTrackNumber ?? 0,
                    Repeat = schedule.MusicRepeat ?? false
                };
            }
        }

        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null || string.IsNullOrEmpty(currentSchedule.MusicPublicationCode))
            return;

        if (current == null)
            current = new AlarmMusic();
        current.TrackNumber = track.Number;
        current.Repeat = track.Repeat;

        var trackSelectedItem = new MusicStateItem
        {
            LanguageCode = currentSchedule.MusicLanguageCode,
            PublicationCode = currentSchedule.MusicPublicationCode,
            SectionCode = PublicationTypeHelper.HasSectionStructure(currentSchedule.MusicPublicationCode)
                ? currentSchedule.MusicSectionCode
                : null,
            TrackNumber = track.Number,
            Repeat = track.Repeat,
            LanguageName = currentSchedule.MusicLanguageName,
            LanguageDirection = currentSchedule.MusicLanguageDirection,
            PublicationName = currentSchedule.MusicPublicationName,
            SectionName = PublicationTypeHelper.HasSectionStructure(currentSchedule.MusicPublicationCode)
                ? currentSchedule.MusicSectionName
                : null,
            TrackName = track.Title
        };

        dispatcher.Dispatch(new TrackSelectedAction(trackSelectedItem));
        await navigationService.PopModalAsync();
    }
}
