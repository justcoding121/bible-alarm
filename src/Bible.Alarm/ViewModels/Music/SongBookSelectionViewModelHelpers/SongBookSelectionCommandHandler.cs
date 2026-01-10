#nullable enable
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.ViewModels.Shared;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Music.SongPublicationSelectionViewModelHelpers;

/// <summary>
/// Handles command execution for SongPublicationSelectionViewModel.
/// </summary>
public sealed class SongPublicationSelectionCommandHandler(
    INavigationService navigationService,
    IState<ApplicationState> state,
    IDispatcher dispatcher)
{
    public async Task HandleTrackSelectionAsync(
        PublicationListViewItemModel songPublication,
        LanguageListViewItemModel? currentLanguage,
        SongPublicationSelectionDataProvider dataProvider,
        AlarmMusic? current)
    {
        if (songPublication == null)
        {
            return;
        }

        var languageCode = currentLanguage?.Code ?? string.Empty;
        if (string.IsNullOrEmpty(languageCode))
        {
            return;
        }

        var currentSchedule = state.Value.CurrentSchedule;
        var (trackNumber, trackName) = await dataProvider.GetTrackForSongPublicationAsync(songPublication, languageCode, currentSchedule);
        if (trackNumber == 0)
        {
            return;
        }

        var trackSelectedItem = CreateMusicStateItemForSongPublication(songPublication, languageCode, trackNumber, trackName, currentLanguage, currentSchedule);
        dispatcher.Dispatch(new TrackSelectedAction(trackSelectedItem));
        await navigationService.PopModalAsync();
    }


    public async Task HandleLanguageSelectionAsync(
        LanguageListViewItemModel language,
        SongPublicationSelectionDataProvider dataProvider,
        Action<LanguageListViewItemModel?> setCurrentLanguage,
        Action<LanguageListViewItemModel> updateSelectedLanguage)
    {
        if (language == null)
        {
            return;
        }

        updateSelectedLanguage(language);

        var currentSchedule = state.Value.CurrentSchedule;
        var (publicationCode, trackNumber, trackName, publicationName) = await dataProvider.GetFirstSongPublicationAndTrackForLanguageAsync(language, currentSchedule);
        if (publicationCode == null)
        {
            return;
        }

        var trackSelectedItem = CreateMusicStateItemForLanguage(language, publicationCode, trackNumber, trackName, publicationName, currentSchedule);
        dispatcher.Dispatch(new TrackSelectedAction(trackSelectedItem));
        await navigationService.PopModalAsync();
    }

    private MusicStateItem CreateMusicStateItemForSongPublication(
        PublicationListViewItemModel songPublication,
        string languageCode,
        int trackNumber,
        string trackName,
        LanguageListViewItemModel? currentLanguage,
        ScheduleStateItem? currentSchedule)
    {
        return new MusicStateItem
        {
            Repeat = currentSchedule?.MusicRepeat ?? false,
            MusicType = MusicType.Vocals,
            LanguageCode = languageCode,
            PublicationCode = songPublication.Code,
            TrackNumber = trackNumber,
            LanguageName = currentLanguage?.Name,
            PublicationName = songPublication.Name,
            TrackName = trackName
        };
    }

    private MusicStateItem CreateMusicStateItemForLanguage(
        LanguageListViewItemModel language,
        string publicationCode,
        int trackNumber,
        string trackName,
        string publicationName,
        ScheduleStateItem? currentSchedule)
    {
        return new MusicStateItem
        {
            Repeat = currentSchedule?.MusicRepeat ?? false,
            MusicType = MusicType.Vocals,
            LanguageCode = language.Code,
            PublicationCode = publicationCode,
            TrackNumber = trackNumber,
            LanguageName = language.Name,
            PublicationName = publicationName,
            TrackName = trackName
        };
    }
}

