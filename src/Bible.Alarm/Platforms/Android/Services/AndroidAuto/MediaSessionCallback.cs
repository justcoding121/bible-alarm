#nullable enable
using Android.OS;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using Bible.Alarm.Common;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;

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

    private bool ValidateScheduleIdExists(int scheduleId)
    {
        var schedules = ApplicationState.Value.Schedules;
        return schedules?.Any(s => s.Id == scheduleId) ?? false;
    }

    private int? GetFirstScheduleId()
    {
        var schedules = ApplicationState.Value.Schedules;
        if (schedules != null && schedules.Count > 0)
        {
            return schedules.First().Id;
        }

        return null;
    }

    public override void OnPlay()
    {
        logger.Information("MediaSessionCallback.OnPlay() called from Android Auto");
        ExecuteAsyncOperation(async () => await HandlePlayAsync());
        base.OnPlay();
    }

    private async Task HandlePlayAsync()
    {
        // Wait for bootstrap to complete before accessing schedules/database
        await MauiProgram.WaitForBootstrapAsync(timeoutMs: 10000);

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
            if (ValidateScheduleIdExists(scheduleId.Value))
            {
                return scheduleId;
            }

            logger.Warning("OnPlay: ScheduleId {ScheduleId} not found in state, using first schedule", scheduleId);
        }

        // Get first schedule from state
        return GetFirstScheduleId();
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
        // Wait for bootstrap to complete before accessing schedules/database
        await MauiProgram.WaitForBootstrapAsync(timeoutMs: 10000);

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
        if (scheduleId > 0 && ValidateScheduleIdExists(scheduleId))
        {
            return scheduleId;
        }

        if (scheduleId > 0)
        {
            logger.Warning("OnPlayFromMediaId: ScheduleId {ScheduleId} not found in state, using first schedule", scheduleId);
        }

        // If scheduleId is invalid or not found, use first schedule from state
        var firstScheduleId = GetFirstScheduleId();
        if (firstScheduleId.HasValue)
        {
            logger.Debug("OnPlayFromMediaId: Using first schedule from state: {ScheduleId}", firstScheduleId);
            return firstScheduleId.Value;
        }

        logger.Warning("OnPlayFromMediaId: No schedules available in state");
        return 0;
    }
}

