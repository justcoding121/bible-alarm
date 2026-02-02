#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.BiblePublications;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Music.MusicSectionSelectionViewModelHelpers;

internal sealed class MusicSectionSelectionCommandHandler
{
    private readonly ILogger logger;
    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly INavigationService navigationService;

    public MusicSectionSelectionCommandHandler(
        ILogger logger,
        IMediaService mediaService,
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        INavigationService navigationService)
    {
        this.logger = logger;
        this.mediaService = mediaService;
        this.state = state;
        this.dispatcher = dispatcher;
        this.navigationService = navigationService;
    }

    public async Task HandleSectionSelectedAsync(
        BiblePublicationSectionListViewItemModel selectedSection,
        Func<bool> isDisposed)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (isDisposed()) return;
            selectedSection.DownloadProgress = 0.0;
        });

        try
        {
            // Always use CurrentSchedule as the source of truth
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null ||
                !currentSchedule.MusicType.HasValue ||
                string.IsNullOrEmpty(currentSchedule.MusicPublicationCode))
            {
                return;
            }

            var musicType = currentSchedule.MusicType.Value;
            var publicationCode = currentSchedule.MusicPublicationCode;
            var languageCode = currentSchedule.MusicLanguageCode; // May be null for instrumental music

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (isDisposed()) return;
                selectedSection.DownloadProgress = 0.3;
            });

            // Get tracks for the selected section
            SortedDictionary<int, MusicTrack> tracks;
            if (musicType == MusicType.VocalMusic && !string.IsNullOrEmpty(languageCode))
            {
                tracks = await mediaService.GetVocalMusicTracks(languageCode, publicationCode);
            }
            else if (musicType == MusicType.Music)
            {
                tracks = await mediaService.GetMelodyMusicTracksBySection(publicationCode, selectedSection.Section.SectionCode);
            }
            else
            {
                return;
            }

            if (tracks == null || tracks.Count == 0)
            {
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (isDisposed()) return;
                selectedSection.DownloadProgress = 0.7;
            });

            // Use the first track from the selected section
            var firstTrack = tracks.Values.First();

            // Create MusicStateItem with selected section and track
            var musicStateItem = new MusicStateItem
            {
                MusicType = musicType,
                LanguageCode = languageCode,
                PublicationCode = publicationCode,
                SectionCode = selectedSection.Section.SectionCode,
                TrackNumber = firstTrack.Number,
                Repeat = currentSchedule.MusicRepeat ?? false,
                // Store display names
                PublicationName = currentSchedule.MusicPublicationName,
                SectionName = selectedSection.Name,
                TrackName = firstTrack.Title
            };

            // Dispatch MusicSectionSelectedAction to update CurrentSchedule
            dispatcher.Dispatch(new MusicSectionSelectedAction(musicStateItem));
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (isDisposed()) return;
                selectedSection.DownloadProgress = 1.0;
            });

            await navigationService.PopModalAsync();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[MusicSectionSelection] TrackSelectionCommand - Error selecting section");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (isDisposed()) return;
                selectedSection.DownloadProgress = 0.0;
            });
        }
    }
}

