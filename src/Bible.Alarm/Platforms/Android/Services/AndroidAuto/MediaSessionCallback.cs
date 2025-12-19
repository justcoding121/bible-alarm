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
    private readonly IPlaybackService _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private IState<ApplicationState>? _applicationState;
    private IState<PlaybackState>? _playbackState;

    private IState<ApplicationState> ApplicationState
    {
        get
        {
            if (_applicationState == null)
            {
                _applicationState = ServiceProviderManager.GetService<IState<ApplicationState>>();
            }
            return _applicationState ?? throw new InvalidOperationException("ApplicationState not available");
        }
    }

    private IState<PlaybackState> PlaybackState
    {
        get
        {
            if (_playbackState == null)
            {
                _playbackState = ServiceProviderManager.GetService<IState<PlaybackState>>();
            }
            return _playbackState ?? throw new InvalidOperationException("PlaybackState not available");
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
        _logger.Information("MediaSessionCallback.OnPlay() called from Android Auto");
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
                _logger.Warning("OnPlay: No valid scheduleId found");
                return;
            }

            await _playbackService.PrepareAndPlayAsync(scheduleId.Value, isAlarm: false);
        }
        else
        {
            // If already playing/paused, just resume
            await _playbackService.PlayAsync();
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

            _logger.Warning("OnPlay: ScheduleId {ScheduleId} not found in state, using first schedule", scheduleId);
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
            _logger.Debug("OnPlay: Got scheduleId {ScheduleId} from MediaSession metadata", parsedScheduleId);
            return parsedScheduleId;
        }

        return null;
    }

    public override void OnPause()
    {
        _logger.Information("MediaSessionCallback.OnPause() called from Android Auto");
        ExecuteAsyncOperation(async () => await _playbackService.PauseAsync());
        base.OnPause();
    }

    public override void OnSkipToNext()
    {
        _logger.Information("MediaSessionCallback.OnSkipToNext() called from Android Auto");
        ExecuteAsyncOperation(async () => await _playbackService.PlayNextAsync());
        base.OnSkipToNext();
    }

    public override void OnSkipToPrevious()
    {
        _logger.Information("MediaSessionCallback.OnSkipToPrevious() called from Android Auto");
        ExecuteAsyncOperation(async () => await _playbackService.PlayPreviousAsync());
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
                    _logger.Error(ex, "Error executing async operation in MediaSessionCallback");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error starting async task in MediaSessionCallback");
        }
    }

    public override void OnPlayFromMediaId(string? mediaId, Bundle? extras)
    {
        _logger.Information("MediaSessionCallback.OnPlayFromMediaId() called with mediaId: {MediaId}", mediaId);

        var parsedScheduleId = ParseMediaId(mediaId);
        ExecuteAsyncOperation(async () => await HandlePlayFromMediaIdAsync(parsedScheduleId));

        base.OnPlayFromMediaId(mediaId, extras);
    }

    private int ParseMediaId(string? mediaId)
    {
        // Parse mediaId directly as scheduleId
        if (!int.TryParse(mediaId, out int scheduleId) || scheduleId <= 0)
        {
            _logger.Warning("MediaSessionCallback.OnPlayFromMediaId() received invalid mediaId: {MediaId}", mediaId);
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
            _logger.Warning("OnPlayFromMediaId: No valid scheduleId found");
            return;
        }

        // Use isAlarm=false for Android Auto playback
        await _playbackService.PrepareAndPlayAsync(scheduleIdToPlay, isAlarm: false);
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
            _logger.Warning("OnPlayFromMediaId: ScheduleId {ScheduleId} not found in state, using first schedule", scheduleId);
        }

        // If scheduleId is invalid or not found, use first schedule from state
        var firstScheduleId = GetFirstScheduleId();
        if (firstScheduleId.HasValue)
        {
            _logger.Debug("OnPlayFromMediaId: Using first schedule from state: {ScheduleId}", firstScheduleId);
            return firstScheduleId.Value;
        }

        _logger.Warning("OnPlayFromMediaId: No schedules available in state");
        return 0;
    }
}

