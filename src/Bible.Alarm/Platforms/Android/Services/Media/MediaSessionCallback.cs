#nullable enable
using Android.OS;
using Android.Support.V4.Media.Session;
using Bible.Alarm.Common;
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

    public override void OnPlay()
    {
        logger.Information("MediaSessionCallback.OnPlay() called - sending PlayButtonPressedMessage");

        // Send weak message instead of direct call
        ExecuteAsyncOperation(async () =>
        {
            WeakReferenceMessenger.Default.Send(new PlayButtonPressedMessage());
            await Task.CompletedTask;
        });

        base.OnPlay();
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
        ExecuteAsyncOperation(async () => await HandlePlayFromMediaIdAsync(parsedScheduleId));

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
    /// Immediately sets MediaSession playback state to buffering when bootstrap is not complete.
    /// Only updates the progress bar to show buffering animation, preserving metadata, controls, and all other state.
    /// Called before waiting for bootstrap to provide immediate feedback while bootstrap is running.
    /// The normal playback flow will handle state changes through MediaSessionEffect when
    /// PlayStatus.Loading is dispatched after bootstrap completes.
    /// Uses AndroidAutoPlayScreenHelper directly without service provider dependency.
    /// </summary>
    private void SetBufferingStateImmediately()
    {
        try
        {
            // Get MediaSession directly from the static helper (no service provider needed)
            var mediaSession = MediaSessionHelper.Create();
            // Only update playback state to buffering, preserving everything else (metadata, controls, etc.)
            AndroidAutoPlayScreenHelper.SetBufferingStateOnly(mediaSession);

            logger.Debug("Set MediaSession playback state to buffering (bootstrap not complete, preserving metadata and controls)");
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to set buffering state immediately");
        }
    }
}

