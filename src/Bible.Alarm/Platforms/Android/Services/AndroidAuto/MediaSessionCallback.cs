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
using Bible.Alarm.Common.Helpers;

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

    public override void OnPlay()
    {
        _logger.Information("MediaSessionCallback.OnPlay() called from Android Auto");
        try
        {
            // Fire and forget - don't block the callback thread
            _ = Task.Run(async () =>
            {
                try
                {
                    // Wait for bootstrap to complete before accessing schedules/database
                    // This ensures Application.Current.Dispatcher is available and database is initialized
                    await MauiProgram.WaitForBootstrapAsync(timeoutMs: 10000);
                    
                    var playbackStateValue = PlaybackState.Value;
                    
                    // If playback is stopped, get scheduleId from metadata or use first schedule
                    if (playbackStateValue.Status == PlayStatus.Stopped)
                    {
                        int? scheduleId = null;
                        
                        // Try to get scheduleId from MediaSession metadata
                        var mediaSessionManager = ServiceProviderManager.GetService<MediaSessionManager>();
                        var mediaSession = mediaSessionManager.GetOrCreate();
                        var mediaId = mediaSession?.Controller?.Metadata?.GetString(MediaMetadataCompat.MetadataKeyMediaId);
                        
                        if (!string.IsNullOrEmpty(mediaId) && int.TryParse(mediaId, out int parsedScheduleId))
                        {
                            scheduleId = parsedScheduleId;
                            _logger.Debug("OnPlay: Got scheduleId {ScheduleId} from MediaSession metadata", scheduleId);
                        }
                        
                        // Validate scheduleId exists in state, or get first schedule
                        if (!scheduleId.HasValue || scheduleId.Value <= 0)
                        {
                            var schedules = ApplicationState.Value.Schedules;
                            if (schedules != null && schedules.Count > 0)
                            {
                                scheduleId = schedules.First().Id;
                                _logger.Debug("OnPlay: Using first schedule from state: {ScheduleId}", scheduleId);
                            }
                            else
                            {
                                _logger.Warning("OnPlay: No schedules available in state");
                                return;
                            }
                        }
                        else
                        {
                            // Validate scheduleId exists in state
                            var schedules = ApplicationState.Value.Schedules;
                            var scheduleExists = schedules?.Any(s => s.Id == scheduleId.Value) ?? false;
                            
                            if (!scheduleExists)
                            {
                                _logger.Warning("OnPlay: ScheduleId {ScheduleId} not found in state, using first schedule", scheduleId);
                                if (schedules != null && schedules.Count > 0)
                                {
                                    scheduleId = schedules.First().Id;
                                }
                                else
                                {
                                    _logger.Warning("OnPlay: No schedules available in state");
                                    return;
                                }
                            }
                        }
                        
                        // Play the schedule
                        await _playbackService.PrepareAndPlayAsync(scheduleId.Value, isAlarm: false);
                    }
                    else
                    {
                        // If already playing/paused, just resume
                        await _playbackService.PlayAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error in MediaSessionCallback.OnPlay()");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error starting play task in MediaSessionCallback.OnPlay()");
        }
        base.OnPlay();
    }

    public override void OnPause()
    {
        _logger.Information("MediaSessionCallback.OnPause() called from Android Auto");
        try
        {
            // Fire and forget - don't block the callback thread
            _ = Task.Run(async () =>
            {
                try
                {
                    await _playbackService.PauseAsync();
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error in MediaSessionCallback.OnPause()");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error starting pause task in MediaSessionCallback.OnPause()");
        }
        base.OnPause();
    }

    public override void OnSkipToNext()
    {
        _logger.Information("MediaSessionCallback.OnSkipToNext() called from Android Auto");
        try
        {
            // Fire and forget - don't block the callback thread
            _ = Task.Run(async () =>
            {
                try
                {
                    await _playbackService.PlayNextAsync();
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error in MediaSessionCallback.OnSkipToNext()");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error starting next task in MediaSessionCallback.OnSkipToNext()");
        }
        base.OnSkipToNext();
    }

    public override void OnSkipToPrevious()
    {
        _logger.Information("MediaSessionCallback.OnSkipToPrevious() called from Android Auto");
        try
        {
            // Fire and forget - don't block the callback thread
            _ = Task.Run(async () =>
            {
                try
                {
                    await _playbackService.PlayPreviousAsync();
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error in MediaSessionCallback.OnSkipToPrevious()");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error starting previous task in MediaSessionCallback.OnSkipToPrevious()");
        }
        base.OnSkipToPrevious();
    }

    public override void OnPlayFromMediaId(string mediaId, Bundle? extras)
    {
        _logger.Information("MediaSessionCallback.OnPlayFromMediaId() called with mediaId: {MediaId}", mediaId);
        try
        {
            // Parse mediaId directly as scheduleId
            if (!int.TryParse(mediaId, out int scheduleId) || scheduleId <= 0)
            {
                _logger.Warning("MediaSessionCallback.OnPlayFromMediaId() received invalid mediaId: {MediaId}", mediaId);
                // Fall through to use first schedule
                scheduleId = 0;
            }
            
            // Fire and forget - don't block the callback thread
            _ = Task.Run(async () =>
            {
                try
                {
                    // Wait for bootstrap to complete before accessing schedules/database
                    // This ensures Application.Current.Dispatcher is available and database is initialized
                    await MauiProgram.WaitForBootstrapAsync(timeoutMs: 10000);
                    
                    int scheduleIdToPlay = scheduleId;
                    
                    // Validate scheduleId exists in state
                    if (scheduleIdToPlay > 0)
                    {
                        var schedules = ApplicationState.Value.Schedules;
                        var scheduleExists = schedules?.Any(s => s.Id == scheduleIdToPlay) ?? false;
                        
                        if (!scheduleExists)
                        {
                            _logger.Warning("OnPlayFromMediaId: ScheduleId {ScheduleId} not found in state, using first schedule", scheduleIdToPlay);
                            scheduleIdToPlay = 0; // Will use first schedule below
                        }
                    }
                    
                    // If scheduleId is invalid or not found, use first schedule from state
                    if (scheduleIdToPlay <= 0)
                    {
                        var schedules = ApplicationState.Value.Schedules;
                        if (schedules != null && schedules.Count > 0)
                        {
                            scheduleIdToPlay = schedules.First().Id;
                            _logger.Debug("OnPlayFromMediaId: Using first schedule from state: {ScheduleId}", scheduleIdToPlay);
                        }
                        else
                        {
                            _logger.Warning("OnPlayFromMediaId: No schedules available in state");
                            return;
                        }
                    }
                    
                    // Use isAlarm=false for Android Auto playback
                    await _playbackService.PrepareAndPlayAsync(scheduleIdToPlay, isAlarm: false);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error in MediaSessionCallback.OnPlayFromMediaId() for scheduleId: {ScheduleId}", scheduleId);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error parsing mediaId in MediaSessionCallback.OnPlayFromMediaId()");
        }
        base.OnPlayFromMediaId(mediaId, extras);
    }
}

