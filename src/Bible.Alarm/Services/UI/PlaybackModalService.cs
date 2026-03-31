#nullable enable
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Services.UI;

public sealed class PlaybackModalService :
    IPlaybackModalService,
    IRecipient<MinimizePlaybackMessage>,
    IRecipient<MaximizePlaybackMessage>,
    IRecipient<RequestShowPlaybackModalMessage>,
    IRecipient<PlaybackExplicitStopMessage>
{
    private readonly ILogger logger;
    private readonly INavigationService navigationService;
    private readonly IState<PlaybackState> playbackState;

    private bool isModalOpen;
    private bool isMinimized;
    private bool isDisposed;
    private int popGeneration;
    private volatile bool requestedShowModal;
    private int? targetScheduleId;
    private bool bypassPopGenerationGuard;

    public bool IsMinimized => isMinimized;

    public PlaybackModalService(
        ILogger logger,
        INavigationService navigationService,
        IState<PlaybackState> playbackState)
    {
        this.logger = logger;
        this.navigationService = navigationService;
        this.playbackState = playbackState;

        WeakReferenceMessenger.Default.Register<MinimizePlaybackMessage>(this);
        WeakReferenceMessenger.Default.Register<MaximizePlaybackMessage>(this);
        WeakReferenceMessenger.Default.Register<RequestShowPlaybackModalMessage>(this);
        WeakReferenceMessenger.Default.Register<PlaybackExplicitStopMessage>(this);
    }

    private static bool IsActiveUiPlaybackStatus(PlayStatus status) =>
        status is PlayStatus.Loading or PlayStatus.Playing or PlayStatus.Paused;

    public void SubscribeToPlaybackStateChanges()
    {
        isModalOpen = false;
        isMinimized = false;
        requestedShowModal = false;
        targetScheduleId = null;
        bypassPopGenerationGuard = false;
        playbackState.StateChanged += OnPlaybackStateChanged;
        logger.Information("SubscribeToPlaybackStateChanges: Subscribed, isModalOpen reset to false");
    }

    public void UnsubscribeToPlaybackStateChanges()
    {
        isModalOpen = false;
        isMinimized = false;
        requestedShowModal = false;
        targetScheduleId = null;
        bypassPopGenerationGuard = false;
        playbackState.StateChanged -= OnPlaybackStateChanged;
    }

    public void Receive(MinimizePlaybackMessage message)
    {
        _ = MinimizeAsync();
    }

    public void Receive(MaximizePlaybackMessage message)
    {
        _ = MaximizeAsync();
    }

    public void Receive(RequestShowPlaybackModalMessage message)
    {
        requestedShowModal = true;
        targetScheduleId = message.TargetScheduleId;

        if (isMinimized)
        {
            var currentScheduleId = playbackState.Value.CurrentScheduleId;
            var isSameSchedule = message.TargetScheduleId.HasValue
                                 && message.TargetScheduleId == currentScheduleId;

            if (isSameSchedule)
            {
                _ = MaximizeAsync();
            }
            else
            {
                // Keep the mini bar visible so it shows stopping UI during the schedule switch,
                // matching the behavior when the user taps Stop on the mini bar.
                // The requestedShowModal guard in OnPlaybackStateChanged prevents
                // HideMiniBarOnMainThreadAsync from hiding it prematurely; MaximizeAsync will
                // hide it when the new schedule starts loading.
                WeakReferenceMessenger.Default.Send(new BeginStoppingPlaybackMessage());
            }
        }
        else if (isModalOpen)
        {
            // Modal is already visible and the same schedule is being resumed (e.g. car tapped
            // now-playing item). No navigation action needed; requestedShowModal is consumed
            // immediately so the guard in OnPlaybackStateChanged stays correct.
            var currentScheduleId = playbackState.Value.CurrentScheduleId;
            var isSameSchedule = message.TargetScheduleId.HasValue
                                 && message.TargetScheduleId == currentScheduleId;

            if (isSameSchedule)
            {
                requestedShowModal = false;
                targetScheduleId = null;
            }
            // Different schedule: leave requestedShowModal = true so the guard in
            // OnPlaybackStateChanged keeps the modal open during the schedule switch.
        }
    }

    public void Receive(PlaybackExplicitStopMessage message)
    {
        logger.Debug("PlaybackExplicitStopMessage received — clearing requestedShowModal and bypassing popGeneration guard for next close");
        requestedShowModal = false;
        targetScheduleId = null;
        bypassPopGenerationGuard = true;
    }

    private Task MinimizeAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                logger.Information("Minimizing playback modal");
                isMinimized = true;
                requestedShowModal = false;
                targetScheduleId = null;

                if (isModalOpen)
                {
                    await navigationService.PopPlaybackPageAsync(animated: false);
                    isModalOpen = false;
                }

                navigationService.SetMiniBarVisible(true);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error minimizing playback modal");
            }
        });
    }

    private Task MaximizeAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                logger.Information("Maximizing playback from mini bar");
                isMinimized = false;
                targetScheduleId = null;

                if (!isModalOpen)
                {
                    await navigationService.OpenPlaybackModalAsync(revealHomeBehindModalOnLoad: true, animated: false);
                    isModalOpen = true;
                }

                navigationService.SetMiniBarVisible(false);

                // Modal is now on screen — tell list rows to clear their play-button spinners.
                WeakReferenceMessenger.Default.Send(new PlaybackModalOpenedMessage());
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error maximizing playback");
                isMinimized = true;
                navigationService.SetMiniBarVisible(true);
            }
        });
    }

    public async Task<bool> ShowPlaybackModalIfNeededOnWindowCreationAsync()
    {
        var modalWasShown = false;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                if (isModalOpen)
                {
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
                    logger.Warning(ex, "PlaybackState not available yet during window creation; using platform playback check fallback");
                }

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
                    navigationService.SetHomePageVisibility(isPlaybackActive: false);
                    return;
                }

                logger.Information(
                    "Window creation - showing PlaybackModal (PlaybackStateAvailable={PlaybackStateAvailable}, Status={Status})",
                    state != null,
                    state?.Status);

#if IOS
                await WaitForWindowReadyAsync();
#endif

                await navigationService.OpenPlaybackModalAsync(revealHomeBehindModalOnLoad: false);
                isModalOpen = true;
                isMinimized = false;
                modalWasShown = true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error showing PlaybackModal during window creation");
                isModalOpen = false;
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
            logger.Warning(ex, "Failed to access PlaybackState; skipping PlaybackModal sync");
            return;
        }

        // Use IsPreparingOrPlaying (resilient to transient Stopped status during track
        // transitions) when the modal or mini bar is already showing. Use the stricter
        // IsActiveUiPlaybackStatus only when deciding whether to auto-open from scratch.
        var shouldShowPlayback = (isModalOpen || isMinimized)
            ? state.IsPreparingOrPlaying
            : IsActiveUiPlaybackStatus(state.Status);

        // When minimized, keep it minimized unless a schedule switch was requested.
        // For schedule switches, maximize when the target schedule starts Loading.
        // Guard against premature maximize: an intermediate Loading event from the old
        // schedule during stop/reset must not consume requestedShowModal before the new
        // schedule fires its own PlaybackStartedAction.
        if (isMinimized && shouldShowPlayback)
        {
            var isTargetScheduleLoading = requestedShowModal
                && state.Status == PlayStatus.Loading
                && (!targetScheduleId.HasValue || state.CurrentScheduleId == targetScheduleId);

            if (isTargetScheduleLoading)
            {
                logger.Information(
                    "OnPlaybackStateChanged: Target schedule loading while minimized — maximizing. Status={Status}, ScheduleId={ScheduleId}, TargetScheduleId={TargetScheduleId}",
                    state.Status, state.CurrentScheduleId, targetScheduleId);
                targetScheduleId = null;
                requestedShowModal = false;
                _ = MaximizeAsync();
            }

            return;
        }

        // During a schedule switch the old schedule stops briefly before the new one
        // starts loading. Keep the modal/bar visible so it doesn't flash.
        // Covers both: modal already open, and bar still showing while MaximizeAsync is queued.
        if (!shouldShowPlayback && (isModalOpen || isMinimized) && requestedShowModal)
        {
            logger.Debug("OnPlaybackStateChanged: Keeping UI visible during schedule switch (requestedShowModal pending). Status={Status}, IsModalOpen={IsModalOpen}, IsMinimized={IsMinimized}",
                state.Status, isModalOpen, isMinimized);
            return;
        }

        // Only auto-open the modal when a user-initiated action (play button, alarm,
        // Android Auto, CarPlay, notification) signalled via RequestShowPlaybackModalMessage.
        var shouldAutoOpen = shouldShowPlayback && !isModalOpen && !isMinimized && requestedShowModal;
        if (shouldShowPlayback && !isModalOpen && !isMinimized && !requestedShowModal)
        {
            logger.Debug("OnPlaybackStateChanged: Playback active but no explicit show request — not auto-opening modal. Status={Status}", state.Status);
            return;
        }

        logger.Debug("OnPlaybackStateChanged: Status={Status}, IsModalOpen={IsModalOpen}, IsMinimized={IsMinimized}, ShouldShow={ShouldShow}",
            state.Status, isModalOpen, isMinimized, shouldShowPlayback);

        if (shouldShowPlayback && isModalOpen)
        {
            // Don't bump popGeneration for Stopped events. During audio player shutdown,
            // PlaybackStatusChangedAction(Stopped) fires before PlaybackStoppedAction clears
            // CurrentScheduleId, making IsPreparingOrPlaying appear true. Bumping here would
            // make ClosePlaybackOnMainThreadAsync look stale and get skipped. Only bump for
            // active (Loading/Playing/Paused) states that represent a new schedule starting.
            if (state.Status != PlayStatus.Stopped)
            {
                popGeneration++;
            }

            // Consume the show-request flag only when the NEW schedule reaches Playing.
            // Don't consume on Paused — the AudioPlayer dispatches a transient Paused
            // during teardown of the old schedule, which would kill the protection
            // before the final Stopped event closes the modal.
            if (state.Status == PlayStatus.Playing && requestedShowModal)
            {
                requestedShowModal = false;
            }
        }

        _ = shouldShowPlayback switch
        {
            true when shouldAutoOpen => MainThread.InvokeOnMainThreadAsync(async () =>
            {
                requestedShowModal = false;

                if (isModalOpen)
                {
                    return;
                }

                isModalOpen = true;
                isMinimized = false;

                try
                {
                    logger.Information(
                        "PlaybackState changed - showing PlaybackModal (Status={Status})",
                        state.Status);

                    await Task.Delay(100);
                    await navigationService.OpenPlaybackModalAsync(revealHomeBehindModalOnLoad: true);

#if IOS
                    if (!navigationService.IsPlaybackModalOnScreen())
                    {
                        logger.Warning("PlaybackModal push was silently ignored by iOS (window not ready) - waiting for window and retrying");
                        await WaitForWindowReadyAsync();

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
                                    return;
                                }
                            }
                            else
                            {
                                logger.Information("Playback no longer active after waiting for window - skipping modal retry");
                                isModalOpen = false;
                                return;
                            }
                        }
                        catch (Exception retryEx)
                        {
                            logger.Warning(retryEx, "Failed to check playback state during modal retry");
                            isModalOpen = false;
                            return;
                        }
                    }
#endif
                    // Modal is now on screen — tell list rows to clear their play-button spinners.
                    WeakReferenceMessenger.Default.Send(new PlaybackModalOpenedMessage());
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error showing PlaybackModal");
                    isModalOpen = false;
                    navigationService.SetHomePageVisibility(isPlaybackActive: false);
                }
            }),
            false when isModalOpen => ClosePlaybackOnMainThreadAsync(),
            false when isMinimized => HideMiniBarOnMainThreadAsync(),
            false when !isModalOpen && !isMinimized => MainThread.InvokeOnMainThreadAsync(() =>
            {
                navigationService.SetHomePageVisibility(isPlaybackActive: false);
                return Task.CompletedTask;
            }),
            _ => Task.CompletedTask
        };
    }

    private Task ClosePlaybackOnMainThreadAsync()
    {
        var capturedGeneration = popGeneration;
        var capturedBypass = bypassPopGenerationGuard;
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            // Skip stale pops caused by schedule switches (new schedule increments popGeneration
            // after the old schedule's stop queues a close). When the user explicitly stopped
            // (capturedBypass = true), always proceed regardless of popGeneration so the modal
            // closes even if a new schedule began loading before this task ran on the main thread.
            if (!capturedBypass && capturedGeneration != popGeneration)
            {
                logger.Information("Skipping stale playback page pop (schedule switched before pop executed)");
                return;
            }

            bypassPopGenerationGuard = false;

            try
            {
                logger.Information("PlaybackState changed - removing PlaybackModal page (Playback inactive). Bypass={Bypass}",
                    capturedBypass);

                requestedShowModal = false;
                targetScheduleId = null;
                navigationService.SetHomePageVisibility(isPlaybackActive: false);
                await navigationService.PopPlaybackPageAsync();
                isModalOpen = false;
                isMinimized = false;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error removing PlaybackModal page");
                isModalOpen = false;
            }
        });
    }

    private Task HideMiniBarOnMainThreadAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            // If MaximizeAsync already opened the modal (race: queued before this),
            // the bar is already hidden and the modal owns the UI. Skip.
            if (isModalOpen)
            {
                logger.Debug("HideMiniBarOnMainThreadAsync: Modal is open (MaximizeAsync ran first), skipping");
                return Task.CompletedTask;
            }

            try
            {
                logger.Information("PlaybackState changed - hiding mini bar (Playback inactive)");
                requestedShowModal = false;
                targetScheduleId = null;
                navigationService.SetMiniBarVisible(false);
                navigationService.SetHomePageVisibility(isPlaybackActive: false);
                isMinimized = false;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error hiding mini bar");
                isMinimized = false;
            }

            return Task.CompletedTask;
        });
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        playbackState.StateChanged -= OnPlaybackStateChanged;
        WeakReferenceMessenger.Default.Unregister<MinimizePlaybackMessage>(this);
        WeakReferenceMessenger.Default.Unregister<MaximizePlaybackMessage>(this);
        WeakReferenceMessenger.Default.Unregister<RequestShowPlaybackModalMessage>(this);
        WeakReferenceMessenger.Default.Unregister<PlaybackExplicitStopMessage>(this);
    }
}
