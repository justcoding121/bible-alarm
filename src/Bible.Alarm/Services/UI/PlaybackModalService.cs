#nullable enable
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Services.UI;

public sealed class PlaybackModalService(
    ILogger logger,
    INavigationService navigationService,
    IState<PlaybackState> playbackState)
    : IPlaybackModalService
{
    private bool isModalOpen;
    private bool isDisposed;

    private static bool IsActiveUiPlaybackStatus(PlayStatus status) =>
        status is PlayStatus.Loading or PlayStatus.Playing or PlayStatus.Paused;

    public void SubscribeToPlaybackStateChanges()
    {
        isModalOpen = false;
        playbackState.StateChanged += OnPlaybackStateChanged;
    }

    public void UnsubscribeToPlaybackStateChanges()
    {
        isModalOpen = false;
        playbackState.StateChanged -= OnPlaybackStateChanged;
    }

    public async Task ShowPlaybackModalIfNeededOnWindowCreationAsync()
    {
        // Must run on UI thread to avoid navigation issues.
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                if (isModalOpen)
                {
                    return;
                }

                PlaybackState? state = null;
                try
                {
                    state = playbackState.Value;
                }
                catch (Exception ex)
                {
                    // Fluxor may not be initialized yet during cold start; don't deadlock.
                    logger.Warning(ex, "PlaybackState not available yet during window creation; using platform playback check fallback");
                }

                // Entry-point strictness (window creation):
                // Only show when playback is already active in UI-relevant states.
                var shouldShow = state != null
                    ? IsActiveUiPlaybackStatus(state.Status)
                    : CheckPlatformPlaybackIsActive();

                if (!shouldShow)
                {
                    // Ensure Home is visible if we aren't showing the modal (defensive).
                    navigationService.SetHomePageVisibility(isPlaybackActive: false);
                    return;
                }

                logger.Information(
                    "Window creation - showing PlaybackModal (PlaybackStateAvailable={PlaybackStateAvailable}, Status={Status})",
                    state != null,
                    state?.Status);

                // Hide Home page before opening modal to prevent visual flash.
                navigationService.SetHomePageVisibility(isPlaybackActive: true);

                // In this entrypoint we keep Home hidden behind the modal.
                await navigationService.OpenPlaybackModalAsync(revealHomeBehindModalOnLoad: false);
                isModalOpen = true;
                // IsBusy cleared by PlaybackModal.OnPageLoaded once rendered.
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error showing PlaybackModal during window creation");
                isModalOpen = false;
                // Defensive: don't leave Home hidden if opening failed.
                navigationService.SetHomePageVisibility(isPlaybackActive: false);
            }
        });
    }

    private bool CheckPlatformPlaybackIsActive()
    {
#if ANDROID
        try
        {
            var mediaSession = Platforms.Android.Services.Media.MediaSessionHelper.Create();
            var sessionPlaybackState = mediaSession?.Controller?.PlaybackState;

            // Active playback states: Playing, Buffering, Paused
            return sessionPlaybackState?.State is
                Android.Support.V4.Media.Session.PlaybackStateCompat.StatePlaying or
                Android.Support.V4.Media.Session.PlaybackStateCompat.StateBuffering or
                Android.Support.V4.Media.Session.PlaybackStateCompat.StatePaused;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to check MediaSession playback state during window creation");
            return false;
        }
#else
        return false;
#endif
    }


    private void OnPlaybackStateChanged(object? sender, EventArgs e)
    {
        PlaybackState state;
        try
        {
            state = playbackState.Value;
        }
        catch (Exception ex)
        {
            // Avoid deadlock / invalid access if Fluxor store isn't ready yet.
            logger.Warning(ex, "Failed to access PlaybackState; skipping PlaybackModal sync");
            return;
        }

        // Strict open rule:
        // - If modal is NOT open yet, only open for Loading/Playing/Paused.
        // - If modal IS already open, keep it open while the playback session is active (IsPreparingOrPlaying),
        //   so we survive brief transitions (e.g., auto-advance, transient stop) and can show errors.
        var shouldShowModal = isModalOpen ? state.IsPreparingOrPlaying : IsActiveUiPlaybackStatus(state.Status);

        _ = shouldShowModal switch
        {
            true when !isModalOpen => MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (isModalOpen)
                {
                    return;
                }

                isModalOpen = true;

                try
                {
                    logger.Information(
                        "PlaybackState changed - showing PlaybackModal (Status={Status})",
                        state.Status);

                    // Yield to allow the schedule list item spinner (IsBusy) to render
                    // before covering the home page with the modal.
                    await Task.Delay(100);

                    // Push modal so it overlays Home. Do NOT hide Home (SetHomePageVisibility true) here -
                    // on iOS that makes the spinner disappear before the modal fully covers, causing a flash.
                    // Spinner clears only via SyncIsBusyWithPlaybackState (stop/close/error/different schedule/timeout).
                    await navigationService.OpenPlaybackModalAsync(revealHomeBehindModalOnLoad: true);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error showing PlaybackModal");
                    // Reset flag on error to allow retry
                    isModalOpen = false;
                    // Defensive: don't leave Home hidden if opening failed.
                    navigationService.SetHomePageVisibility(isPlaybackActive: false);
                }
            }),
            false when isModalOpen => MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    logger.Information("PlaybackState changed - hiding PlaybackModal (Playback inactive)");

                    // Show Home page before closing modal
                    navigationService.SetHomePageVisibility(isPlaybackActive: false);

                    await navigationService.PopModalAsync();
                    isModalOpen = false;
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error hiding PlaybackModal");
                    // Force reset the flag even on error - the modal may have been closed externally
                    isModalOpen = false;
                }
            }),
            // Defensive: If playback stopped but isModalOpen is false, ensure Home is visible
            false when !isModalOpen => MainThread.InvokeOnMainThreadAsync(() =>
            {
                // Ensure Home page is visible when playback stops (defensive)
                navigationService.SetHomePageVisibility(isPlaybackActive: false);
                return Task.CompletedTask;
            }),
            _ => Task.CompletedTask
        };
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // Unsubscribe from playback state changes
        playbackState.StateChanged -= OnPlaybackStateChanged;
    }
}

