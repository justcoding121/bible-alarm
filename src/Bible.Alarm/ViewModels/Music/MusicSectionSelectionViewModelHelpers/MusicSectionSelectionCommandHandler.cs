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
        Action<bool> setIsBusy,
        Action<bool> setShowProgress,
        Action<double> setProgressPercent,
        Action<string> setProgressText,
        Func<bool> isDisposed,
        Func<bool> isSelectingSection)
    {
        // Track start time to ensure minimum display duration
        var startTime = DateTime.UtcNow;
        const int minimumDisplayMs = 800; // Minimum time to show progress indicator

        // Show progress immediately on UI thread before any async work
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (isDisposed()) return;
            setIsBusy(true);
            setShowProgress(true);
            setProgressPercent(0.0);
            setProgressText("Loading...");
        });

        // Give UI thread enough time to render the progress indicator
        await Task.Delay(300);

        try
        {
            // Always use CurrentSchedule as the source of truth
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null ||
                !currentSchedule.MusicType.HasValue ||
                string.IsNullOrEmpty(currentSchedule.MusicPublicationCode))
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (isDisposed()) return;
                    setIsBusy(false);
                    setShowProgress(false);
                });
                return;
            }

            var musicType = currentSchedule.MusicType.Value;
            var publicationCode = currentSchedule.MusicPublicationCode;
            var languageCode = currentSchedule.MusicLanguageCode; // May be null for instrumental music

            // Update progress
            setProgressPercent(0.3);
            setProgressText("Checking tracks...");

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
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (isDisposed()) return;
                    setIsBusy(false);
                    setShowProgress(false);
                });
                return;
            }

            if (tracks == null || tracks.Count == 0)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (isDisposed()) return;
                    setIsBusy(false);
                    setShowProgress(false);
                });
                return;
            }

            // Update progress
            setProgressPercent(0.7);
            setProgressText("Completing...");

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

            // Ensure minimum display time has elapsed
            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            if (elapsed < minimumDisplayMs)
            {
                var remaining = minimumDisplayMs - (int)elapsed;
                await Task.Delay(remaining);
            }
            else
            {
                await Task.Delay(200); // Brief delay to show completion
            }

            setProgressPercent(1.0);
            setProgressText("100%");
            await Task.Delay(100);

            // Keep IsBusy=true until modal closes - don't hide busy overlay here
            await navigationService.PopModalAsync();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[MusicSectionSelection] TrackSelectionCommand - Error selecting section");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (isDisposed()) return;
                // Only reset IsBusy if we’re not in the middle of selection/navigation
                if (!isSelectingSection())
                {
                    setIsBusy(false);
                }
                setShowProgress(false);
            });
        }
    }
}

