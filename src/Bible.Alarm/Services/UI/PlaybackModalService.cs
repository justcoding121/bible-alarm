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
    private int popGeneration;

    private static bool IsActiveUiPlaybackStatus(PlayStatus status) =>
        status is PlayStatus.Loading or PlayStatus.Playing or PlayStatus.Paused;

    public void SubscribeToPlaybackStateChanges()
    {
        isModalOpen = false;
        playbackState.StateChanged += OnPlaybackStateChanged;
        logger.Information("SubscribeToPlaybackStateChanges: Subscribed, isModalOpen reset to false");
    }

    public void UnsubscribeToPlaybackStateChanges()
    {
        isModalOpen = false;
        playbackState.StateChanged -= OnPlaybackStateChanged;
    }

    public async Task<bool> ShowPlaybackModalIfNeededOnWindowCreationAsync()
    {
        var modalWasShown = false;

        // Must run on UI thread to avoid navigation issues.
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                if (isModalOpen)
                {
                    // Modal was already opened by OnPlaybackStateChanged (race between
                    // SubscribeToPlaybackStateChanges and InitializedMessage). Treat as
                    // "modal shown" so the caller doesn't push Home on iOS (which would
                    // deadlock PushAsync while a modal is presented).
                    logger.Information("ShowPlaybackModalIfNeededOnWindowCreationAsync - modal already open (opened by state change listener)");
                    modalWasShown = true;
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

                logger.Information(
                    "ShowPlaybackModalIfNeededOnWindowCreationAsync - PlaybackStateAvailable={PlaybackStateAvailable}, Status={Status}, ShouldShow={ShouldShow}",
                    state != null,
                    state?.Status,
                    shouldShow);

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

#if IOS
                // On iOS, the root view controller hasn't completed its appearance cycle
                // (viewDidAppear) yet when this runs during window creation. iOS silently
                // ignores PresentViewController calls on a VC that hasn't appeared.
                // Poll until the window is ready rather than using a fixed delay, since
                // in cold-start-from-notification scenarios the window may not exist yet.
                await WaitForWindowReadyAsync();
#endif

                // Home stays visible (opacity 1) behind the modal during cold start.
                // Setting opacity to 0 on Android prevents CollectionView from laying out correctly.
                await navigationService.OpenPlaybackModalAsync(revealHomeBehindModalOnLoad: false);
                isModalOpen = true;
                modalWasShown = true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error showing PlaybackModal during window creation");
                isModalOpen = false;
                // Defensive: don't leave Home hidden if opening failed.
                navigationService.SetHomePageVisibility(isPlaybackActive: false);
            }
        });

        return modalWasShown;
    }

#if IOS
    private async Task WaitForWindowReadyAsync()
    {
        const int maxWaitMs = 10000;
        const int pollIntervalMs = 100;
        var elapsed = 0;

        while (elapsed < maxWaitMs)
        {
            var window = Application.Current?.Windows.FirstOrDefault();
            var page = window?.Page;
            if (page?.Handler?.PlatformView != null)
            {
                var vc = (page.Handler as IPlatformViewHandler)?.ViewController;
                if (vc?.IsViewLoaded == true && vc.View?.Window != null)
                {
                    // Root VC view is loaded and attached to a UIWindow.
                    // However, iOS silently ignores PresentViewController until
                    // viewDidAppear fires. Yield long enough for the appearance
                    // cycle to complete (cold start from CarPlay + notification
                    // can delay appearance significantly).
                    logger.Debug("WaitForWindowReadyAsync: Window ready after {ElapsedMs}ms, waiting for appearance cycle", elapsed);
                    await Task.Delay(1000);
                    return;
                }
            }

            await Task.Delay(pollIntervalMs);
            elapsed += pollIntervalMs;
        }

        logger.Warning("WaitForWindowReadyAsync timed out after {MaxWaitMs}ms - attempting modal push anyway", maxWaitMs);
    }
#endif

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
#elif IOS
        try
        {
            var audioPlayer = Common.ServiceProviderManager.GetService<Services.Media.Interfaces.IAudioPlayer>();
            if (audioPlayer != null)
            {
                var isActive = audioPlayer.IsActuallyPlayingOrPaused;
                logger.Information("iOS platform playback check: IsActuallyPlayingOrPaused={IsActive}", isActive);
                return isActive;
            }

            logger.Debug("iOS platform playback check: IAudioPlayer not available");
            return false;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to check AudioPlayer state during window creation on iOS");
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

        logger.Debug("OnPlaybackStateChanged: Status={Status}, IsModalOpen={IsModalOpen}, ShouldShow={ShouldShow}",
            state.Status, isModalOpen, shouldShowModal);

        // When switching schedules, PlaybackStoppedAction and PlaybackStartedAction fire in rapid
        // succession on the same thread. The pop from PlaybackStoppedAction is queued on the main
        // thread but hasn't executed yet when PlaybackStartedAction fires. Increment a generation
        // counter so the queued pop can detect it's stale and skip the close.
        if (shouldShowModal && isModalOpen)
        {
            popGeneration++;
        }

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

#if IOS
                    // iOS silently ignores PresentViewController when the root VC hasn't
                    // completed its appearance cycle (viewDidAppear). If the modal push was
                    // silently dropped, wait for the window to be ready and retry.
                    if (!navigationService.IsPlaybackModalOnScreen())
                    {
                        logger.Warning("PlaybackModal push was silently ignored by iOS (window not ready) - waiting for window and retrying");
                        await WaitForWindowReadyAsync();

                        // Re-check that playback is still active before retrying
                        try
                        {
                            var currentState = playbackState.Value;
                            if (IsActiveUiPlaybackStatus(currentState.Status))
                            {
                                await navigationService.OpenPlaybackModalAsync(revealHomeBehindModalOnLoad: true);

                                if (!navigationService.IsPlaybackModalOnScreen())
                                {
                                    logger.Error("PlaybackModal push failed again after waiting for window ready");
                                    isModalOpen = false;
                                    navigationService.SetHomePageVisibility(isPlaybackActive: false);
                                }
                            }
                            else
                            {
                                logger.Information("Playback no longer active after waiting for window - skipping modal retry");
                                isModalOpen = false;
                            }
                        }
                        catch (Exception retryEx)
                        {
                            logger.Warning(retryEx, "Failed to check playback state during modal retry");
                            isModalOpen = false;
                        }
                    }
#endif
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
            false when isModalOpen => CloseModalOnMainThreadAsync(),
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

    /// <summary>
    /// Captures the current pop generation and closes the modal on the main thread.
    /// If another schedule started between when the close was queued and when it executes,
    /// the generation will have advanced and the stale pop is skipped.
    /// </summary>
    private Task CloseModalOnMainThreadAsync()
    {
        var capturedGeneration = popGeneration;
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (capturedGeneration != popGeneration)
            {
                logger.Information("Skipping stale modal pop (schedule switched before pop executed)");
                return;
            }

            try
            {
                logger.Information("PlaybackState changed - hiding PlaybackModal (Playback inactive)");

                navigationService.SetHomePageVisibility(isPlaybackActive: false);

                await navigationService.PopModalAsync();
                isModalOpen = false;

                // On iOS cold start, Home was NOT pushed during initialization (PushAsync
                // hangs when a modal is presented). Push it now that the modal is gone.
                // NavigateToHomeAsync is a no-op if Home is already in the stack.
                await navigationService.NavigateToHomeAsync(animated: false);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error hiding PlaybackModal");
                isModalOpen = false;
            }
        });
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

