#nullable enable
using Android.OS;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using Bible.Alarm.Common;
using Bible.Alarm.Platforms.Android.Effects;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Platforms.Android.Services.AndroidAuto;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Stores;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Media;

/// <summary>
/// Callback handler for MediaSessionCompat commands.
/// This handles play, pause, next, previous, and other media button events.
/// </summary>
public class MediaSessionCallback(IPlaybackService playbackService, ILogger logger) : MediaSessionCompat.Callback
{
    private readonly IPlaybackService playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private IState<ApplicationState>? applicationState;
    private IState<PlaybackState>? playbackState;

    private IState<ApplicationState> ApplicationState
    {
        get
        {
            if (applicationState == null)
            {
                applicationState = ServiceProviderManager.GetService<IState<ApplicationState>>();
            }
            return applicationState ?? throw new InvalidOperationException("ApplicationState not available");
        }
    }

    private IState<PlaybackState> PlaybackState
    {
        get
        {
            if (playbackState == null)
            {
                playbackState = ServiceProviderManager.GetService<IState<PlaybackState>>();
            }
            return playbackState ?? throw new InvalidOperationException("PlaybackState not available");
        }
    }

    private IDefaultScheduleService? defaultScheduleService;

    private IDefaultScheduleService DefaultScheduleService
    {
        get
        {
            if (defaultScheduleService == null)
            {
                defaultScheduleService = ServiceProviderManager.GetService<IDefaultScheduleService>();
            }
            return defaultScheduleService ?? throw new InvalidOperationException("DefaultScheduleService not available");
        }
    }

    private Interfaces.IMediaSessionManager? mediaSessionManager;

    private Interfaces.IMediaSessionManager MediaSessionManager
    {
        get
        {
            if (mediaSessionManager == null)
            {
                mediaSessionManager = ServiceProviderManager.GetService<Interfaces.IMediaSessionManager>();
            }
            return mediaSessionManager ?? throw new InvalidOperationException("MediaSessionManager not available");
        }
    }

    public override void OnPlay()
    {
        logger.Information("MediaSessionCallback.OnPlay() called");

        var pState = PlaybackState;

        // If there's an active playback session (paused/playing/loading), resume
        // normally like any media player. This preserves playlist position, avoids
        // restarting music intro, and lets PlaybackOperationHandler handle stale
        // ExoPlayer state (re-prepares current track with seek when resources lost).
        if (pState?.Value?.IsPreparingOrPlaying == true)
        {
            logger.Information("MediaSessionCallback.OnPlay() - active session exists (Status={Status}), resuming via PlayButtonPressedMessage",
                pState.Value.Status);
            ExecuteAsyncOperation(async () =>
            {
                WeakReferenceMessenger.Default.Send(new PlayButtonPressedMessage());
                await Task.CompletedTask;
            });
            base.OnPlay();
            return;
        }

        // No active playback (default metadata / stopped state) - do a clean restart
        // from the database with full playlist preparation.
        // Don't call SetBufferingStateImmediately() here: changing from Paused → Buffering
        // immediately hides the progress bar/time area on Android Auto, causing visible
        // text bounce (subtitle and time shift up then back down).
        // Instead, rely on:
        //   - ExecuteAsyncOperation's bootstrap-pending path (calls SetBufferingStateImmediately inside Task.Run)
        //   - HandlePlaybackStatusChanged(Loading) to set Buffering via Fluxor when loading actually starts
        // This keeps the existing metadata/time visible until the track actually starts loading.
        MediaSessionEffect.SetRestartingPlayback(true);

        ExecuteAsyncOperation(async () =>
        {
            try
            {
                var scheduleIdFromMetadata = TryGetScheduleIdFromMediaSession();
                if (scheduleIdFromMetadata.HasValue && scheduleIdFromMetadata.Value > 0)
                {
                    logger.Information("MediaSessionCallback.OnPlay() - fresh start using schedule {ScheduleId} from metadata",
                        scheduleIdFromMetadata.Value);
                    await playbackService.PrepareAndPlayAsync(scheduleIdFromMetadata.Value, isAlarm: false);
                }
                else
                {
                    var firstId = DefaultScheduleService.GetFirstScheduleId();
                    if (firstId.HasValue && firstId.Value > 0)
                    {
                        logger.Information("MediaSessionCallback.OnPlay() - fresh start using first schedule {ScheduleId}",
                            firstId.Value);
                        await playbackService.PrepareAndPlayAsync(firstId.Value, isAlarm: false);
                    }
                    else
                    {
                        logger.Warning("MediaSessionCallback.OnPlay() - no schedule ID available, sending PlayButtonPressedMessage");
                        WeakReferenceMessenger.Default.Send(new PlayButtonPressedMessage());
                    }
                }
            }
            catch
            {
                MediaSessionEffect.SetRestartingPlayback(false);
                throw;
            }
        });

        base.OnPlay();
    }

    private int? TryGetScheduleIdFromMediaSession()
    {
        try
        {
            var mediaSession = MediaSessionHelper.Create();
            var mediaId = mediaSession.Controller?.Metadata?.GetString(MediaMetadataCompat.MetadataKeyMediaId);
            if (!string.IsNullOrEmpty(mediaId) && int.TryParse(mediaId, out var scheduleId) && scheduleId > 0)
            {
                return scheduleId;
            }
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Could not get schedule ID from MediaSession metadata");
        }
        return null;
    }

    public override void OnPause()
    {
        logger.Information("MediaSessionCallback.OnPause() called - sending PauseButtonPressedMessage");
        ExecuteAsyncOperation(async () =>
        {
            WeakReferenceMessenger.Default.Send(new PauseButtonPressedMessage());
            await Task.CompletedTask;
        });
        base.OnPause();
    }

    public override void OnSkipToNext()
    {
        logger.Information("MediaSessionCallback.OnSkipToNext() called - sending NextButtonPressedMessage");
        ExecuteAsyncOperation(async () =>
        {
            WeakReferenceMessenger.Default.Send(new NextButtonPressedMessage());
            await Task.CompletedTask;
        });
        base.OnSkipToNext();
    }

    public override void OnSkipToPrevious()
    {
        logger.Information("MediaSessionCallback.OnSkipToPrevious() called - sending PreviousButtonPressedMessage");
        ExecuteAsyncOperation(async () =>
        {
            WeakReferenceMessenger.Default.Send(new PreviousButtonPressedMessage());
            await Task.CompletedTask;
        });
        base.OnSkipToPrevious();
    }

    public override void OnFastForward()
    {
        logger.Information("MediaSessionCallback.OnFastForward() called - sending SeekForwardButtonPressedMessage");
        ExecuteAsyncOperation(async () =>
        {
            WeakReferenceMessenger.Default.Send(new SeekForwardButtonPressedMessage());
            await Task.CompletedTask;
        });
        base.OnFastForward();
    }

    public override void OnRewind()
    {
        logger.Information("MediaSessionCallback.OnRewind() called - sending SeekBackwardButtonPressedMessage");
        ExecuteAsyncOperation(async () =>
        {
            WeakReferenceMessenger.Default.Send(new SeekBackwardButtonPressedMessage());
            await Task.CompletedTask;
        });
        base.OnRewind();
    }

    private void ExecuteAsyncOperation(Func<Task> asyncOperation)
    {
        try
        {
            // Fire and forget - don't block the callback thread
            _ = Task.Run(async () =>
            {
                try
                {
                    // Show buffering feedback while waiting for bootstrap
                    if (!BootstrapHelper.IsBootstrapCompleted())
                    {
                        SetBufferingStateImmediately();
                    }

                    // Wait for bootstrap to complete (returns immediately if already complete)
                    try
                    {
                        await MauiProgram.WaitForBootstrapAsync();
                    }
                    catch (TimeoutException timeoutEx)
                    {
                        // Bootstrap timed out - proceed anyway, operation may still work
                        // (e.g. PlaybackService falls back to Preferences for schedule ID)
                        logger?.Warning(timeoutEx, "Bootstrap wait timed out in MediaSessionCallback - proceeding with operation anyway");
                    }

                    await asyncOperation();
                }
                catch (Exception ex)
                {
                    logger?.Error(ex, "Error executing async operation in MediaSessionCallback");
                }
            });
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error starting async task in MediaSessionCallback");
        }
    }

    public override void OnPlayFromMediaId(string? mediaId, Bundle? extras)
    {
        logger.Information("MediaSessionCallback.OnPlayFromMediaId() called with mediaId: {MediaId}", mediaId);

        var parsedScheduleId = ParseMediaId(mediaId);

        // If this schedule is already playing/paused, just resume like OnPlay() does.
        // Setting buffering state here would get stuck because PrepareAndPlayAsync
        // exits early for the same schedule without dispatching a new Playing status,
        // leaving lastSetState pinned to Buffering indefinitely.
        var pState = PlaybackState;
        if (pState?.Value?.IsPreparingOrPlaying == true
            && pState.Value.CurrentScheduleId == parsedScheduleId)
        {
            logger.Information(
                "OnPlayFromMediaId: Schedule {ScheduleId} is already active (Status={Status}) — resuming via PlayButtonPressedMessage",
                parsedScheduleId, pState.Value.Status);
            ExecuteAsyncOperation(async () =>
            {
                WeakReferenceMessenger.Default.Send(new PlayButtonPressedMessage());
                await Task.CompletedTask;
            });
            base.OnPlayFromMediaId(mediaId, extras);
            return;
        }

        // Different schedule or no active playback — do a full stop-and-restart.
        // Suppress intermediate Stopped/Ended state + default metadata during the
        // transition to prevent play/pause button flash and artwork blink.
        MediaSessionEffect.SetRestartingPlayback(true);
        SetBufferingStateImmediately();

        ExecuteAsyncOperation(async () =>
        {
            try
            {
                await HandlePlayFromMediaIdAsync(parsedScheduleId);
            }
            catch
            {
                MediaSessionEffect.SetRestartingPlayback(false);
                throw;
            }
        });

        base.OnPlayFromMediaId(mediaId, extras);
    }

    private int ParseMediaId(string? mediaId)
    {
        // Parse mediaId directly as scheduleId
        if (!int.TryParse(mediaId, out int scheduleId) || scheduleId <= 0)
        {
            logger.Warning("MediaSessionCallback.OnPlayFromMediaId() received invalid mediaId: {MediaId}", mediaId);
            return 0; // Will use first schedule
        }

        return scheduleId;
    }

    private async Task HandlePlayFromMediaIdAsync(int scheduleId)
    {
        var scheduleIdToPlay = GetValidScheduleId(scheduleId);
        if (scheduleIdToPlay <= 0)
        {
            logger.Warning("OnPlayFromMediaId: No valid scheduleId found");
            return;
        }

        // Use isAlarm=false for media session playback
        await playbackService.PrepareAndPlayAsync(scheduleIdToPlay, isAlarm: false);
    }

    private int GetValidScheduleId(int scheduleId)
    {
        // Validate scheduleId exists in state
        if (scheduleId > 0 && DefaultScheduleService.ValidateScheduleIdExists(scheduleId))
        {
            return scheduleId;
        }

        if (scheduleId > 0)
        {
            logger.Warning("OnPlayFromMediaId: ScheduleId {ScheduleId} not found in state, using first schedule", scheduleId);
        }

        // If scheduleId is invalid or not found, use first schedule from state
        var firstScheduleId = DefaultScheduleService.GetFirstScheduleId();
        if (firstScheduleId.HasValue)
        {
            logger.Debug("OnPlayFromMediaId: Using first schedule from state: {ScheduleId}", firstScheduleId);
            return firstScheduleId.Value;
        }

        logger.Warning("OnPlayFromMediaId: No schedules available in state");
        return 0;
    }

    /// <summary>
    /// Immediately sets MediaSession playback state to buffering.
    /// Routes through IMediaSessionManager so lastSetState is updated, preventing
    /// stale position updates from pushing an old state back onto the session.
    /// Falls back to AndroidAutoPlayScreenHelper when DI is not available (pre-bootstrap).
    /// </summary>
    private void SetBufferingStateImmediately()
    {
        try
        {
            try
            {
                MediaSessionManager.SetBufferingStateOnly();
                logger.Debug("Set MediaSession playback state to buffering via MediaSessionManager");
                return;
            }
            catch (InvalidOperationException)
            {
                // DI not available yet (pre-bootstrap) — fall through to direct call
            }

            var mediaSession = MediaSessionHelper.Create();
            AndroidAutoPlayScreenHelper.SetBufferingStateOnly(mediaSession);
            logger.Debug("Set MediaSession playback state to buffering via AndroidAutoPlayScreenHelper (pre-bootstrap fallback)");
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to set buffering state immediately");
        }
    }
}

