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
    IRecipient<MaximizePlaybackMessage>
{
    private readonly ILogger logger;
    private readonly INavigationService navigationService;
    private readonly IState<PlaybackState> playbackState;

    private bool isModalOpen;
    private bool isMinimized;
    private bool isDisposed;
    private int popGeneration;

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
    }

    private static bool IsActiveUiPlaybackStatus(PlayStatus status) =>
        status is PlayStatus.Loading or PlayStatus.Playing or PlayStatus.Paused;

    public void SubscribeToPlaybackStateChanges()
    {
        isModalOpen = false;
        isMinimized = false;
        playbackState.StateChanged += OnPlaybackStateChanged;
        logger.Information("SubscribeToPlaybackStateChanges: Subscribed, isModalOpen reset to false");
    }

    public void UnsubscribeToPlaybackStateChanges()
    {
        isModalOpen = false;
        isMinimized = false;
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

    private Task MinimizeAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                logger.Information("Minimizing playback modal");
                isMinimized = true;

                if (isModalOpen)
                {
                    await navigationService.PopPlaybackPageAsync();
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
                navigationService.SetMiniBarVisible(false);

                if (!isModalOpen)
                {
                    await navigationService.OpenPlaybackModalAsync(revealHomeBehindModalOnLoad: true);
                    isModalOpen = true;
                }
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

        var shouldShowPlayback = isModalOpen
            ? state.IsPreparingOrPlaying
            : IsActiveUiPlaybackStatus(state.Status);

        // When minimized, the mini bar is showing. If playback is still active, keep it minimized.
        // Only open the full modal for NEW playback sessions (not when already minimized).
        if (isMinimized && shouldShowPlayback)
        {
            return;
        }

        logger.Debug("OnPlaybackStateChanged: Status={Status}, IsModalOpen={IsModalOpen}, IsMinimized={IsMinimized}, ShouldShow={ShouldShow}",
            state.Status, isModalOpen, isMinimized, shouldShowPlayback);

        if (shouldShowPlayback && isModalOpen)
        {
            popGeneration++;
        }

        _ = shouldShowPlayback switch
        {
            true when !isModalOpen && !isMinimized => MainThread.InvokeOnMainThreadAsync(async () =>
            {
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
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (capturedGeneration != popGeneration)
            {
                logger.Information("Skipping stale playback page pop (schedule switched before pop executed)");
                return;
            }

            try
            {
                logger.Information("PlaybackState changed - removing PlaybackModal page (Playback inactive)");

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
            try
            {
                logger.Information("PlaybackState changed - hiding mini bar (Playback inactive)");
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
    }
}
