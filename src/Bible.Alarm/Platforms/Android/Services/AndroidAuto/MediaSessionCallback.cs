#nullable enable
using Android.OS;
using Android.Support.V4.Media.Session;
using Bible.Alarm.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto;

/// <summary>
/// Callback handler for MediaSessionCompat commands from Android Auto.
/// This handles play, pause, next, previous, and other media button events.
/// </summary>
public class MediaSessionCallback : MediaSessionCompat.Callback
{
    private readonly IPlaybackService _playbackService;
    private readonly ILogger _logger;

    public MediaSessionCallback(IPlaybackService playbackService, ILogger logger)
    {
        _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                    await _playbackService.PlayAsync();
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
            if (int.TryParse(mediaId, out int scheduleId))
            {
                // Fire and forget - don't block the callback thread
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Use isAlarm=false for Android Auto playback
                        await _playbackService.PrepareAndPlayAsync(scheduleId, isAlarm: false);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error in MediaSessionCallback.OnPlayFromMediaId() for scheduleId: {ScheduleId}", scheduleId);
                    }
                });
            }
            else
            {
                _logger.Warning("MediaSessionCallback.OnPlayFromMediaId() received invalid mediaId: {MediaId}", mediaId);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error parsing mediaId in MediaSessionCallback.OnPlayFromMediaId()");
        }
        base.OnPlayFromMediaId(mediaId, extras);
    }
}

