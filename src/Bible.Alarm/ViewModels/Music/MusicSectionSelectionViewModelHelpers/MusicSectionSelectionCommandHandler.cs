#nullable enable

using System.Net.Http;
using Bible.Alarm.Common;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
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
        // Progress will only be set if a fetch actually happens (not for DB-only queries)
        try
        {
            // Always use CurrentSchedule as the source of truth
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null ||
                string.IsNullOrEmpty(currentSchedule.MusicPublicationCode))
            {
                return;
            }

            var publicationCode = currentSchedule.MusicPublicationCode;
            // May be null for instrumental music
            var languageCode = currentSchedule.MusicLanguageCode;
            // Music type is inferred: NULL/empty LanguageCode = instrumental (melody)
            var isMelodyMusic = string.IsNullOrEmpty(languageCode);

            // Get tracks for the selected section (DB query only, no fetch)
            SortedDictionary<int, MusicTrack> tracks;
            if (!isMelodyMusic)
            {
                // Vocal music
                tracks = await mediaService.GetVocalMusicTracks(languageCode!, publicationCode);
            }
            else
            {
                // Instrumental/melody music
                tracks = await mediaService.GetMelodyMusicTracksBySection(publicationCode, selectedSection.Section.SectionCode);
            }

            if (tracks == null || tracks.Count == 0)
            {
                return;
            }

            // Use the first track from the selected section
            var firstTrack = tracks.Values.First();

            // Create MusicStateItem with selected section and track
            var musicStateItem = new MusicStateItem
            {
                LanguageCode = languageCode,
                PublicationCode = publicationCode,
                SectionCode = selectedSection.Section.SectionCode,
                TrackCode = firstTrack.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Repeat = currentSchedule.MusicRepeat ?? false,
                // Store display names
                PublicationName = currentSchedule.MusicPublicationName,
                SectionName = selectedSection.Name,
                TrackName = firstTrack.Title
            };

            // Dispatch MusicSectionSelectedAction to update CurrentSchedule
            dispatcher.Dispatch(new MusicSectionSelectedAction(musicStateItem));

            await navigationService.PopModalAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Net.Sockets.SocketException or TaskCanceledException)
        {
            logger.Warning(ex, "[MusicSectionSelection] TrackSelectionCommand - Network error selecting section");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (isDisposed()) return;
                selectedSection.DownloadProgress = 0.0;
            });
            var toastService = ServiceProviderManager.GetService<IToastService>();
            await toastService.ShowMessage("Unable to load. Please check your connection.");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[MusicSectionSelection] TrackSelectionCommand - Error selecting section");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (isDisposed()) return;
                selectedSection.DownloadProgress = 0.0;
            });
            var toastService = ServiceProviderManager.GetService<IToastService>();
            await toastService.ShowMessage("An error occurred. Please try again.");
        }
    }
}

