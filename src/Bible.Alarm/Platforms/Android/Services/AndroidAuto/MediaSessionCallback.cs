#nullable enable
using Android.OS;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;
using Application = Android.App.Application;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto;

/// <summary>
/// Callback handler for MediaSessionCompat commands from Android Auto.
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
        logger.Information("MediaSessionCallback.OnPlay() called from Android Auto");

        ExecuteAsyncOperation(HandlePlayAsync);
        base.OnPlay();
    }

    private async Task HandlePlayAsync()
    {
        var playbackStateValue = PlaybackState.Value;

        // If playback is stopped, get scheduleId from metadata or use first schedule
        if (playbackStateValue.Status == PlayStatus.Stopped)
        {
            var scheduleId = GetScheduleIdForPlay();
            if (!scheduleId.HasValue)
            {
                logger.Warning("OnPlay: No valid scheduleId found");
                return;
            }

            await playbackService.PrepareAndPlayAsync(scheduleId.Value, isAlarm: false);
        }
        else
        {
            // If already playing/paused, just resume
            await playbackService.PlayAsync();
        }
    }

    private int? GetScheduleIdForPlay()
    {
        // Try to get scheduleId from MediaSession metadata
        var scheduleId = GetScheduleIdFromMetadata();
        if (scheduleId.HasValue)
        {
            // Validate scheduleId exists in state
            if (DefaultScheduleService.ValidateScheduleIdExists(scheduleId.Value))
            {
                return scheduleId;
            }

            logger.Warning("OnPlay: ScheduleId {ScheduleId} not found in state, using first schedule", scheduleId);
        }

        // Get first schedule from state
        return DefaultScheduleService.GetFirstScheduleId();
    }

    private int? GetScheduleIdFromMetadata()
    {
        var mediaSessionManager = ServiceProviderManager.GetService<MediaSessionManager>();
        var mediaSession = mediaSessionManager?.GetOrCreate();
        var mediaId = mediaSession?.Controller?.Metadata?.GetString(MediaMetadataCompat.MetadataKeyMediaId);

        if (!string.IsNullOrEmpty(mediaId) && int.TryParse(mediaId, out int parsedScheduleId))
        {
            logger.Debug("OnPlay: Got scheduleId {ScheduleId} from MediaSession metadata", parsedScheduleId);
            return parsedScheduleId;
        }

        return null;
    }

    public override void OnPause()
    {
        logger.Information("MediaSessionCallback.OnPause() called from Android Auto");
        ExecuteAsyncOperation(async () => await playbackService.PauseAsync());
        base.OnPause();
    }

    public override void OnSkipToNext()
    {
        logger.Information("MediaSessionCallback.OnSkipToNext() called from Android Auto");
        ExecuteAsyncOperation(async () => await playbackService.PlayNextAsync());
        base.OnSkipToNext();
    }

    public override void OnSkipToPrevious()
    {
        logger.Information("MediaSessionCallback.OnSkipToPrevious() called from Android Auto");
        ExecuteAsyncOperation(async () => await playbackService.PlayPreviousAsync());
        base.OnSkipToPrevious();
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
                    // Always wait for bootstrap to complete before executing the operation
                    // Check if bootstrap is complete first - if not, set buffering state
                    if (!BootstrapHelper.IsBootstrapCompleted())
                    {
                        // Set buffering state to show immediate feedback while waiting for bootstrap
                        SetBufferingStateImmediately();
                    }

                    // Wait for bootstrap to complete (returns immediately if already complete)
                    await MauiProgram.WaitForBootstrapAsync();

                    // Execute the callback-specific operation after bootstrap is complete
                    await asyncOperation();
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error executing async operation in MediaSessionCallback");
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

        // Use isAlarm=false for Android Auto playback
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
            var mediaSession = AndroidAutoMediaSessionHelper.Create();
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

