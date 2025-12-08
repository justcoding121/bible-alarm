#nullable enable
using Android.OS;
using Android.Support.V4.Media.Session;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto;

/// <summary>
/// Callback handler for MediaSessionCompat commands from Android Auto.
/// This handles play, pause, next, previous, and other media button events.
/// </summary>
public class MediaSessionCallback : MediaSessionCompat.Callback
{
    private static readonly ILogger Logger = Log.ForContext<MediaSessionCallback>();

    public override void OnPlay()
    {
        Logger.Information("MediaSessionCallback.OnPlay() called from Android Auto");
        // TODO: Integrate with your playback service
        // Example: _playbackService.PlayAsync();
        base.OnPlay();
    }

    public override void OnPause()
    {
        Logger.Information("MediaSessionCallback.OnPause() called from Android Auto");
        // TODO: Integrate with your playback service
        // Example: _playbackService.PauseAsync();
        base.OnPause();
    }

    public override void OnSkipToNext()
    {
        Logger.Information("MediaSessionCallback.OnSkipToNext() called from Android Auto");
        // TODO: Integrate with your playback service
        // Example: _playbackService.NextAsync();
        base.OnSkipToNext();
    }

    public override void OnSkipToPrevious()
    {
        Logger.Information("MediaSessionCallback.OnSkipToPrevious() called from Android Auto");
        // TODO: Integrate with your playback service
        // Example: _playbackService.PreviousAsync();
        base.OnSkipToPrevious();
    }

    public override void OnPlayFromMediaId(string mediaId, Bundle? extras)
    {
        Logger.Information("MediaSessionCallback.OnPlayFromMediaId() called with mediaId: {MediaId}", mediaId);
        // TODO: Integrate with your playback service
        // Example: _playbackService.PlayScheduleAsync(mediaId);
        base.OnPlayFromMediaId(mediaId, extras);
    }
}

