#nullable enable
using Android.Support.V4.Media.Session;
using Bible.Alarm.Platforms.Android.Services.AndroidAuto;
using Bible.Alarm.Services.Media.Models;

namespace Bible.Alarm.Platforms.Android.Services.Media.MediaSessionManagerHelpers;

/// <summary>
/// Handles playback state management for MediaSession.
/// </summary>
public sealed class PlaybackStateManager
{
    /// <summary>
    /// Builds playback actions based on navigation availability.
    /// </summary>
    public long BuildPlaybackActions(bool canPlayNext, bool canPlayPrevious)
    {
        // Base actions that are always available
        long actions = PlaybackStateCompat.ActionPlay |
                       PlaybackStateCompat.ActionPause |
                       PlaybackStateCompat.ActionPlayPause |
                       PlaybackStateCompat.ActionPlayFromMediaId;

        // Next/Previous are always available.
        actions |= PlaybackStateCompat.ActionSkipToNext;
        actions |= PlaybackStateCompat.ActionSkipToPrevious;

        return actions;
    }

    /// <summary>
    /// Creates a playback state from parameters.
    /// </summary>
    public PlaybackStateCompat? CreatePlaybackState(int state, long position, float playbackSpeed, long actions)
    {
        return AndroidAutoPlayScreenHelper.CreatePlaybackState(state, position, playbackSpeed, actions);
    }

    /// <summary>
    /// Creates a playback state from an existing one with updated position.
    /// </summary>
    public PlaybackStateCompat? CreatePlaybackStateFromExisting(PlaybackStateCompat playbackState, long positionMs, long actions)
    {
        return AndroidAutoPlayScreenHelper.CreatePlaybackStateFromExisting(playbackState, positionMs, actions);
    }

    /// <summary>
    /// Checks if playback is currently active.
    /// </summary>
    public bool IsPlaybackActive(PlaybackStateCompat playbackState)
    {
        return playbackState.State is PlaybackStateCompat.StatePlaying or
               PlaybackStateCompat.StateBuffering or
               PlaybackStateCompat.StatePaused;
    }

    /// <summary>
    /// Maps PlayStatus to PlaybackStateCompat state.
    /// </summary>
    public int MapPlayStatusToState(PlayStatus status)
    {
        // Stopped/Ended use StatePaused (not StateStopped) to stay consistent with
        // SetStoppedState, which uses StatePaused to hint Android Auto that media is
        // "ready" rather than "unavailable". This prevents conflicting state updates
        // between handlers that would cause rapid play/pause button flashing.
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

    /// <summary>
    /// Sets buffering state only on the MediaSession.
    /// </summary>
    public void SetBufferingStateOnly(MediaSessionCompat mediaSession)
    {
        AndroidAutoPlayScreenHelper.SetBufferingStateOnly(mediaSession);
    }

    /// <summary>
    /// Sets stopped state on the MediaSession.
    /// </summary>
    public void SetStoppedState(MediaSessionCompat mediaSession)
    {
        AndroidAutoPlayScreenHelper.SetStoppedState(mediaSession);
    }
}
