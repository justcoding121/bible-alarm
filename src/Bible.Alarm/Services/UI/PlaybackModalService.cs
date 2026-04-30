#nullable enable
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Playback;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.UI;

public sealed partial class PlaybackModalService :
    IPlaybackModalService,
    IRecipient<MinimizePlaybackMessage>,
    IRecipient<MaximizePlaybackMessage>,
    IRecipient<RequestShowPlaybackModalMessage>,
    IRecipient<PlaybackExplicitStopMessage>
{
    private readonly ILogger logger;
    private readonly INavigationService navigationService;
    private readonly IState<PlaybackState> playbackState;
    private readonly IAudioPlayer audioPlayer;
    private readonly IDispatcher dispatcher;

    private volatile bool isModalOpen;
    private volatile bool isMinimized;
    private bool isDisposed;
    private int popGeneration;
    private volatile bool requestedShowModal;
    private int? targetScheduleId;
    private volatile bool bypassPopGenerationGuard;
    private DateTime lastMinimizedAtUtc;
    private const int MinimizeCooldownMs = 500;

    public bool IsMinimized => isMinimized;

    public bool IsModalOpenOrPending => isModalOpen || requestedShowModal;

    public bool WasRecentlyMinimized() =>
        isMinimized && (DateTime.UtcNow - lastMinimizedAtUtc).TotalMilliseconds < MinimizeCooldownMs;

    public PlaybackModalService(
        ILogger logger,
        INavigationService navigationService,
        IState<PlaybackState> playbackState,
        IAudioPlayer audioPlayer,
        IDispatcher dispatcher)
    {
        this.logger = logger;
        this.navigationService = navigationService;
        this.playbackState = playbackState;
        this.audioPlayer = audioPlayer;
        this.dispatcher = dispatcher;

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
        navigationService.SetMiniBarVisible(false);
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

        ScheduleModalSafetyCheck();
    }

    public void Receive(PlaybackExplicitStopMessage message)
    {
        logger.Debug("PlaybackExplicitStopMessage received — clearing requestedShowModal and bypassing popGeneration guard for next close");
        requestedShowModal = false;
        targetScheduleId = null;
        bypassPopGenerationGuard = true;

        if (isModalOpen)
        {
            _ = ClosePlaybackUiOnExplicitStopAsync();
        }
    }

    private Task ClosePlaybackUiOnExplicitStopAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                logger.Information("ClosePlaybackUiOnExplicitStopAsync: Immediately closing playback UI");
                navigationService.SetMiniBarVisible(false);

                if (isModalOpen || navigationService.IsPlaybackModalOnScreen())
                {
                    await navigationService.PopPlaybackPageAsync();
                }

                isModalOpen = false;
                isMinimized = false;
                requestedShowModal = false;
                targetScheduleId = null;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in ClosePlaybackUiOnExplicitStopAsync");
                isModalOpen = false;
                isMinimized = false;
            }
        });
    }

    private Task MinimizeAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                logger.Information("Minimizing playback modal");
                isMinimized = true;
                lastMinimizedAtUtc = DateTime.UtcNow;
                requestedShowModal = false;
                targetScheduleId = null;

                if (isModalOpen)
                {
                    await navigationService.PopPlaybackPageAsync(animated: false);

                    if (navigationService.IsPlaybackModalOnScreen())
                    {
                        logger.Warning("MinimizeAsync: PopPlaybackPageAsync returned but modal is still on screen — retrying");
                        await Task.Delay(100);
                        await navigationService.PopPlaybackPageAsync(animated: false);
                    }

                    if (navigationService.IsPlaybackModalOnScreen())
                    {
                        logger.Error("MinimizeAsync: Modal still on screen after retry — aborting minimize to prevent zombie state");
                        isMinimized = false;
                        return;
                    }

                    isModalOpen = false;
                }

                navigationService.SetMiniBarVisible(true);

                // Playback session is now established via the mini bar. Clear any list-item
                // spinners that are waiting for PlaybackModalOpenedMessage — the modal won't
                // re-open (or was already open during a schedule switch), so this is the
                // only signal they will receive.
                WeakReferenceMessenger.Default.Send(new PlaybackModalOpenedMessage());
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
                if (!IsPlayerActuallyActive())
                {
                    // During a schedule switch the audio player is legitimately inactive while
                    // tracks are being prepared, but Fluxor already has IsPreparingOrPlaying=true
                    // from PlaybackStartedAction. Trust Fluxor and proceed with the maximize
                    // instead of dismissing as zombie.
                    var isFluxorActive = false;
                    try
                    {
                        isFluxorActive = playbackState.Value.IsPreparingOrPlaying;
                    }
                    catch (Exception ex)
                    {
                        logger.Warning(ex, "Failed to read PlaybackState during maximize");
                    }

                    if (!isFluxorActive)
                    {
                        logger.Warning("Maximize requested but player is inactive — dismissing playback UI");
                        await DismissZombiePlaybackUiAsync();
                        return;
                    }

                    logger.Information("Maximize: player inactive but Fluxor IsPreparingOrPlaying — proceeding (track preparation in progress)");
                }

                logger.Information("Maximizing playback from mini bar");
                isMinimized = false;
                targetScheduleId = null;

                if (!isModalOpen)
                {
                    await navigationService.OpenPlaybackModalAsync(animated: false);

                    if (!navigationService.IsPlaybackModalOnScreen())
                    {
                        logger.Warning("MaximizeAsync: PlaybackModal push failed — keeping mini bar visible");
                        isMinimized = true;
                        navigationService.SetMiniBarVisible(true);
                        WeakReferenceMessenger.Default.Send(new PlaybackModalOpenedMessage());
                        return;
                    }

                    isModalOpen = true;
                }

                navigationService.SetMiniBarVisible(false);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error maximizing playback — keeping mini bar visible");
                isMinimized = true;
                navigationService.SetMiniBarVisible(true);
                WeakReferenceMessenger.Default.Send(new PlaybackModalOpenedMessage());
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
                    : CheckPlatformPlaybackIsActive(logger);

                logger.Information(
                    "ShowPlaybackModalIfNeededOnWindowCreationAsync - PlaybackStateAvailable={PlaybackStateAvailable}, Status={Status}, ShouldShow={ShouldShow}",
                    state != null,
                    state?.Status,
                    shouldShow);

                if (!shouldShow)
                {
                    return;
                }

                logger.Information(
                    "Window creation - showing PlaybackModal (PlaybackStateAvailable={PlaybackStateAvailable}, Status={Status})",
                    state != null,
                    state?.Status);

#if IOS
                await WaitForWindowReadyAsync();
#endif

                // Show mini bar immediately so the home page reflects active playback
                // while the modal page is being prepared and pushed.
                // Hidden again once the modal is on screen.
                navigationService.SetMiniBarVisible(true);
                await Task.Delay(150);
                await navigationService.OpenPlaybackModalAsync();

                if (!navigationService.IsPlaybackModalOnScreen())
                {
                    logger.Warning("ShowPlaybackModalIfNeededOnWindowCreationAsync: push failed — keeping mini bar visible");
                    isMinimized = true;
                    modalWasShown = true;
                    return;
                }

                isModalOpen = true;
                isMinimized = false;
                navigationService.SetMiniBarVisible(false);
                modalWasShown = true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error showing PlaybackModal during window creation — keeping mini bar visible");
                isModalOpen = false;
                navigationService.SetMiniBarVisible(true);
                isMinimized = true;
            }
        });

        return modalWasShown;
    }

    public async Task ShowPlaybackModalIfNeededOnResumeAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(ShowPlaybackModalIfNeededOnResumeCoreAsync);
    }

    private async Task ShowPlaybackModalIfNeededOnResumeCoreAsync()
    {
        try
        {
            if (!ShouldContinueResumeFlowAfterModalStackCheck())
            {
                return;
            }

            if (isMinimized)
            {
                navigationService.SetMiniBarVisible(true);
                return;
            }

            PlaybackState? state = null;
            try
            {
                state = playbackState.Value;
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "PlaybackState not available during resume check; using platform playback check fallback");
            }

            var shouldShow = state != null
                ? IsActiveUiPlaybackStatus(state.Status)
                : CheckPlatformPlaybackIsActive(logger);

            if (!shouldShow)
            {
                return;
            }

            logger.Information(
                "ShowPlaybackModalIfNeededOnResumeAsync: Playback active (Status={Status}) but no UI visible — showing modal",
                state?.Status);

            navigationService.SetMiniBarVisible(true);
            await Task.Delay(150);
            await navigationService.OpenPlaybackModalAsync();

            if (!navigationService.IsPlaybackModalOnScreen())
            {
                logger.Warning("ShowPlaybackModalIfNeededOnResumeAsync: push failed — falling back to mini bar");
                FallBackToMiniBar();
                return;
            }

            isModalOpen = true;
            isMinimized = false;
            navigationService.SetMiniBarVisible(false);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error showing PlaybackModal on resume — falling back to mini bar");
            FallBackToMiniBar();
        }
    }

    /// <summary>
    /// Returns false when the modal is already visible (nothing to do). Otherwise resets a stale
    /// <see cref="isModalOpen"/> flag when the page is missing from the stack, then returns true.
    /// </summary>
    private bool ShouldContinueResumeFlowAfterModalStackCheck()
    {
        if (!isModalOpen)
        {
            return true;
        }

        if (navigationService.IsPlaybackModalOnScreen())
        {
            return false;
        }

        logger.Warning(
            "ShowPlaybackModalIfNeededOnResumeAsync: isModalOpen was true but PlaybackModal is not on navigation stack — resetting state");
        isModalOpen = false;
        return true;
    }

    public void ShowMiniBarIfPlaybackActiveOnResume()
    {
        try
        {
            if (isModalOpen && navigationService.IsPlaybackModalOnScreen())
            {
                return;
            }

            if (isMinimized)
            {
                navigationService.SetMiniBarVisible(true);
                return;
            }

            PlaybackState? state = null;
            try
            {
                state = playbackState.Value;
            }
            catch (Exception)
            {
                // Fluxor not ready; fall through to platform check
            }

            var isActive = state != null
                ? IsActiveUiPlaybackStatus(state.Status)
                : CheckPlatformPlaybackIsActive(logger);

            if (isActive)
            {
                logger.Information(
                    "ShowMiniBarIfPlaybackActiveOnResume: Playback active (Status={Status}) — showing mini bar as preview while modal is prepared",
                    state?.Status);
                navigationService.SetMiniBarVisible(true);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error in ShowMiniBarIfPlaybackActiveOnResume");
        }
    }

#if IOS
    private async Task WaitForWindowReadyAsync()
    {
        const int maxWaitMs = 10000;
        const int pollIntervalMs = 100;
        var elapsed = 0;

        while (elapsed < maxWaitMs)
        {
            var window = Application.Current?.Windows is { Count: > 0 } wins ? wins[0] : null;
            var page = window?.Page;
            if (page?.Handler?.PlatformView != null)
            {
                var vc = (page.Handler as IPlatformViewHandler)?.ViewController;
                if (vc?.IsViewLoaded is true && vc.View?.Window != null)
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

    private static bool CheckPlatformPlaybackIsActive(ILogger logger)
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
        _ = logger;
        return false;
#endif
    }

    private bool TryGetPlaybackStateForModalSync(out PlaybackState state)
    {
        try
        {
            state = playbackState.Value;
            return true;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to access PlaybackState; skipping PlaybackModal sync");
            state = null!;
            return false;
        }
    }

    private static bool ComputeShouldShowPlaybackForModal(bool modalOrMiniVisible, PlaybackState state) =>
        modalOrMiniVisible
            ? state.IsPreparingOrPlaying
            : IsActiveUiPlaybackStatus(state.Status);

    private bool TryHandleMinimizedActivePlaybackBranch(PlaybackState state, bool shouldShowPlayback)
    {
        if (!isMinimized || !shouldShowPlayback)
        {
            return false;
        }

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

        return true;
    }

    private bool TryPreserveShowModalDuringTransientInactive(PlaybackState state, bool shouldShowPlayback)
    {
        if (shouldShowPlayback || !requestedShowModal)
        {
            return false;
        }

        logger.Debug("OnPlaybackStateChanged: Preserving requestedShowModal during transient inactive state. Status={Status}, IsModalOpen={IsModalOpen}, IsMinimized={IsMinimized}",
            state.Status, isModalOpen, isMinimized);
        return true;
    }

    private bool TryEnqueueMiniBarSafetyNetAndReturn(PlaybackState state, bool shouldShowPlayback)
    {
        if (!shouldShowPlayback || isModalOpen || isMinimized || requestedShowModal || bypassPopGenerationGuard)
        {
            return false;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!isModalOpen && !isMinimized)
            {
                logger.Information(
                    "OnPlaybackStateChanged: Playback active with no UI visible — showing mini bar as safety net. Status={Status}",
                    state.Status);
                isMinimized = true;
                navigationService.SetMiniBarVisible(true);
                WeakReferenceMessenger.Default.Send(new PlaybackModalOpenedMessage());
            }
        });
        return true;
    }

    private void ApplyPopGenerationWhenModalOpen(PlaybackState state, bool shouldShowPlayback)
    {
        if (!shouldShowPlayback || !isModalOpen)
        {
            return;
        }

        if (state.Status != PlayStatus.Stopped)
        {
            popGeneration++;
        }

        if (state.Status == PlayStatus.Playing && requestedShowModal)
        {
            requestedShowModal = false;
            WeakReferenceMessenger.Default.Send(new PlaybackModalOpenedMessage());
        }
    }

    private async Task AutoOpenPlaybackModalOnMainThreadAsync(PlaybackState state)
    {
        if (!App.IsInForeground)
        {
            logger.Information(
                "Auto-open deferred — app is in background (Status={Status}). Resume handler will show modal when foregrounded.",
                state.Status);
            return;
        }

        requestedShowModal = false;

        if (isModalOpen)
        {
            return;
        }

        isModalOpen = true;
        isMinimized = false;
        navigationService.SetMiniBarVisible(false);

        try
        {
            logger.Information(
                "PlaybackState changed - showing PlaybackModal (Status={Status})",
                state.Status);

            await Task.Delay(100);
            await navigationService.OpenPlaybackModalAsync();

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
                        await navigationService.OpenPlaybackModalAsync();
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

            if (!navigationService.IsPlaybackModalOnScreen())
            {
                logger.Warning("PlaybackModal push failed — modal not on navigation stack. Falling back to mini bar");
                FallBackToMiniBar();
                return;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error showing PlaybackModal — falling back to mini bar");
            FallBackToMiniBar();
        }
    }

    private Task ClearOrphanedModalWhenNoFlagsAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            requestedShowModal = false;
            targetScheduleId = null;
            bypassPopGenerationGuard = false;
            navigationService.SetMiniBarVisible(false);

            if (navigationService.IsPlaybackModalOnScreen())
            {
                logger.Warning("OnPlaybackStateChanged: Orphaned modal detected (no flags set but modal on screen) — popping");
                await navigationService.PopPlaybackPageAsync();
            }
        });
    }

    private void DispatchPlaybackVisibilitySwitch(bool shouldShowPlayback, bool shouldAutoOpen, PlaybackState state)
    {
        _ = shouldShowPlayback switch
        {
            true when shouldAutoOpen => MainThread.InvokeOnMainThreadAsync(() => AutoOpenPlaybackModalOnMainThreadAsync(state)),
            false when isMinimized => HideMiniBarOnMainThreadAsync(),
            false when isModalOpen => ClosePlaybackOnMainThreadAsync(),
            false when !isModalOpen && !isMinimized => ClearOrphanedModalWhenNoFlagsAsync(),
            _ => Task.CompletedTask
        };
    }

    private void OnPlaybackStateChanged(object? sender, EventArgs e)
    {
        if (!TryGetPlaybackStateForModalSync(out var state))
        {
            return;
        }

        var shouldShowPlayback = ComputeShouldShowPlaybackForModal(isModalOpen || isMinimized, state);
        if (TryHandleMinimizedActivePlaybackBranch(state, shouldShowPlayback))
        {
            return;
        }

        if (TryPreserveShowModalDuringTransientInactive(state, shouldShowPlayback))
        {
            return;
        }

        if (TryEnqueueMiniBarSafetyNetAndReturn(state, shouldShowPlayback))
        {
            return;
        }

        logger.Debug("OnPlaybackStateChanged: Status={Status}, IsModalOpen={IsModalOpen}, IsMinimized={IsMinimized}, ShouldShow={ShouldShow}",
            state.Status, isModalOpen, isMinimized, shouldShowPlayback);

        ApplyPopGenerationWhenModalOpen(state, shouldShowPlayback);

        var shouldAutoOpen = shouldShowPlayback && !isModalOpen && !isMinimized && requestedShowModal;
        DispatchPlaybackVisibilitySwitch(shouldShowPlayback, shouldAutoOpen, state);
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
                navigationService.SetMiniBarVisible(false);
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
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            // If MaximizeAsync already opened the modal (race: queued before this),
            // the bar is already hidden and the modal owns the UI. Skip.
            if (isModalOpen)
            {
                logger.Debug("HideMiniBarOnMainThreadAsync: Modal is open (MaximizeAsync ran first), skipping");
                return;
            }

            if (requestedShowModal)
            {
                logger.Debug("HideMiniBarOnMainThreadAsync: requestedShowModal is true — keeping mini bar visible during schedule switch");
                return;
            }

            try
            {
                logger.Information("PlaybackState changed - hiding mini bar (Playback inactive)");
                requestedShowModal = false;
                targetScheduleId = null;
                bypassPopGenerationGuard = false;
                navigationService.SetMiniBarVisible(false);
                isMinimized = false;

                if (navigationService.IsPlaybackModalOnScreen())
                {
                    logger.Warning("HideMiniBarOnMainThreadAsync: Orphaned modal detected (isModalOpen=false but modal on screen) — popping");
                    await navigationService.PopPlaybackPageAsync();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error hiding mini bar");
                isMinimized = false;
            }
        });
    }

    /// <summary>
    /// Resets modal state and falls back to the mini playback bar when the
    /// PlaybackModal push fails or the page is not on the navigation stack.
    /// Also sends PlaybackModalOpenedMessage so list-item spinners are cleared.
    /// Must be called on the main thread.
    /// </summary>
    private void FallBackToMiniBar()
    {
        isModalOpen = false;
        isMinimized = true;
        navigationService.SetMiniBarVisible(true);
        WeakReferenceMessenger.Default.Send(new PlaybackModalOpenedMessage());
    }

    private const int ModalSafetyCheckDelayMs = 10_000;

    /// <summary>
    /// After a show-modal request, schedules a delayed check. If the modal or mini bar
    /// still hasn't appeared, forces the playback modal (with mini bar fallback).
    /// Catches edge cases where OnPlaybackStateChanged never fires or the auto-open
    /// silently fails.
    /// </summary>
    private void ScheduleModalSafetyCheck()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(ModalSafetyCheckDelayMs);

                if (isMinimized)
                {
                    return;
                }

                await MainThread.InvokeOnMainThreadAsync(RunModalSafetyCheckOnMainThreadAsync);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error in modal safety check");
            }
        });
    }

    private async Task RunModalSafetyCheckOnMainThreadAsync()
    {
        if (isMinimized)
        {
            return;
        }

        ResetModalOpenIfNotOnNavigationStack();

        if (!requestedShowModal)
        {
            return;
        }

        if (!App.IsInForeground)
        {
            logger.Debug("Modal safety check: app is backgrounded — deferring to resume handler");
            return;
        }

        PlaybackState? state = null;
        try
        {
            state = playbackState.Value;
        }
        catch (Exception)
        {
            // Fluxor not ready
        }

        var isActive = state != null
            ? IsActiveUiPlaybackStatus(state.Status)
            : CheckPlatformPlaybackIsActive(logger);

        if (!isActive)
        {
            logger.Debug("Modal safety check: playback is no longer active — clearing stale requestedShowModal");
            requestedShowModal = false;
            targetScheduleId = null;
            return;
        }

        logger.Warning(
            "Modal safety check: requestedShowModal pending >{DelayMs}ms with no UI visible (Status={Status}) — forcing playback modal",
            ModalSafetyCheckDelayMs, state?.Status);

        requestedShowModal = false;

        try
        {
            await navigationService.OpenPlaybackModalAsync();

            if (navigationService.IsPlaybackModalOnScreen())
            {
                isModalOpen = true;
                isMinimized = false;
                navigationService.SetMiniBarVisible(false);
                WeakReferenceMessenger.Default.Send(new PlaybackModalOpenedMessage());
                return;
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Modal safety check: modal push failed");
        }

        FallBackToMiniBar();
    }

    private void ResetModalOpenIfNotOnNavigationStack()
    {
        if (!isModalOpen)
        {
            return;
        }

        if (navigationService.IsPlaybackModalOnScreen())
        {
            return;
        }

        logger.Warning("Modal safety check: isModalOpen was true but PlaybackModal is not on navigation stack — resetting");
        isModalOpen = false;
    }

    /// <summary>
    /// Checks whether the underlying audio player is actually in an active state
    /// (playing, paused, or buffering). Returns false when the player has been
    /// disposed or stopped but the Fluxor state was not cleaned up.
    /// </summary>
    private bool IsPlayerActuallyActive()
    {
        try
        {
            if (audioPlayer.IsActuallyPlayingOrPaused)
            {
                return true;
            }

            return audioPlayer.Status is PlayStatus.Loading;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error checking player active state");
            return false;
        }
    }

    /// <summary>
    /// Cleans up playback UI (mini bar and/or modal) and dispatches PlaybackStoppedAction
    /// when the player is dead but the UI is still showing. Prevents the user from seeing
    /// a zombie modal with all controls disabled.
    /// </summary>
    private async Task DismissZombiePlaybackUiAsync()
    {
        try
        {
            requestedShowModal = false;
            targetScheduleId = null;
            navigationService.SetMiniBarVisible(false);

            if (isModalOpen)
            {
                await navigationService.PopPlaybackPageAsync();
            }

            isModalOpen = false;
            isMinimized = false;

            dispatcher.Dispatch(new PlaybackStoppedAction());
#if ANDROID || IOS
            dispatcher.Dispatch(new SetCarPlayScreenAction());
#endif
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error dismissing zombie playback UI");
            isModalOpen = false;
            isMinimized = false;
        }
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
