#nullable enable
using Android.Support.V4.Media.Session;
using Bible.Alarm.Platforms.Android.Services.AndroidAuto;
using Bible.Alarm.Services.Media.Models;

namespace Bible.Alarm.Platforms.Android.Services.Media.MediaSessionManagerHelpers;

/// <summary>
/// Playback state helpers for MediaSession (pure functions — no instance state).
/// </summary>
public static class PlaybackStateManager
{
    /// <summary>
    /// Builds playback actions based on navigation availability.
    /// </summary>
    public static long BuildPlaybackActions(bool canPlayNext, bool canPlayPrevious)
    {
        long actions = PlaybackStateCompat.ActionPlay |
                       PlaybackStateCompat.ActionPause |
                       PlaybackStateCompat.ActionPlayPause |
                       PlaybackStateCompat.ActionPlayFromMediaId;

        actions |= PlaybackStateCompat.ActionSkipToNext;
        actions |= PlaybackStateCompat.ActionSkipToPrevious;

        return actions;
    }

    public static PlaybackStateCompat? CreatePlaybackState(int state, long position, float playbackSpeed, long actions)
    {
        return AndroidAutoPlayScreenHelper.CreatePlaybackState(state, position, playbackSpeed, actions);
    }

    public static PlaybackStateCompat? CreatePlaybackStateFromExisting(PlaybackStateCompat playbackState, long positionMs, long actions)
    {
        return AndroidAutoPlayScreenHelper.CreatePlaybackStateFromExisting(playbackState, positionMs, actions);
    }

    public static bool IsPlaybackActive(PlaybackStateCompat playbackState)
    {
        return playbackState.State is PlaybackStateCompat.StatePlaying or
               PlaybackStateCompat.StateBuffering or
               PlaybackStateCompat.StatePaused;
    }

    public static int MapPlayStatusToState(PlayStatus status)
    {
        return status switch
        {
            PlayStatus.Playing => PlaybackStateCompat.StatePlaying,
            PlayStatus.Paused => PlaybackStateCompat.StatePaused,
            PlayStatus.Loading => PlaybackStateCompat.StateBuffering,
            PlayStatus.Stopped => PlaybackStateCompat.StatePaused,
            PlayStatus.Ended => PlaybackStateCompat.StatePaused,
            PlayStatus.Failed => PlaybackStateCompat.StateError,
            _ => PlaybackStateCompat.StateNone
        };
    }

    public static void SetBufferingStateOnly(MediaSessionCompat mediaSession)
    {
        AndroidAutoPlayScreenHelper.SetBufferingStateOnly(mediaSession);
    }

    public static void SetStoppedState(MediaSessionCompat mediaSession)
    {
        AndroidAutoPlayScreenHelper.SetStoppedState(mediaSession);
    }
}
