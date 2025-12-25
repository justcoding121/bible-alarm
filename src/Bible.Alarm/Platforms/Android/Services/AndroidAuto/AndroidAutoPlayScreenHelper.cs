#nullable enable
using Android.Content;
using Android.OS;
using Android.Support.V4.Media.Session;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto;

/// <summary>
/// Helper for managing Android Auto play screen UI states and playback states.
/// Intentionally does NOT depend on MAUI DI/services.
/// Centralizes all PlaybackStateCompat.Builder creation logic.
/// </summary>
public static class AndroidAutoPlayScreenHelper
{
    /// <summary>
    /// Applies blank loading state (buffering with progress bar animation).
    /// Optionally clears metadata and sets STATE_BUFFERING to indicate player is initializing.
    /// Follows standard Media Resumption patterns (like YouTube Music).
    /// </summary>
    /// <param name="mediaSession">The MediaSessionCompat instance</param>
    /// <param name="clearMetaData">If true, clears metadata. If false, preserves existing metadata and only animates progress bar.</param>
    public static void ApplyBlankLoadingState(MediaSessionCompat mediaSession, bool clearMetaData = true)
    {
        // Clear metadata (no artwork, no title/subtitle) + set buffering state.
        // Following standard Media Resumption patterns (like YouTube Music):
        // - Use STATE_BUFFERING instead of STATE_NONE to indicate player is initializing
        // - This tells Android Auto that the app is responsive and ready, avoiding blank screens
        // - Keep session active so Android Auto can see the session
        if (clearMetaData)
        {
            try
            {
                // Clear metadata - no artwork, no text
                mediaSession?.SetMetadata(null);
            }
            catch
            {
                mediaSession?.SetMetadata(null);
            }
        }

        var playbackState = CreatePlaybackState(
            PlaybackStateCompat.StateBuffering,
            position: 0,
            actions: PlaybackStateCompat.ActionPlay);

        if (playbackState != null)
        {
            mediaSession?.SetPlaybackState(playbackState);
        }

        // Keep session active so Android Auto can see the session and show proper UI
        if (mediaSession != null)
        {
            mediaSession.Active = true;
        }
    }

    /// <summary>
    /// Sets buffering state with no controls (disables all actions).
    /// Used during transitions when switching schedules before metadata is ready.
    /// </summary>
    public static void SetBufferingNoControlsState(MediaSessionCompat mediaSession)
    {
        // Clear metadata directly (don't call ApplyBlankLoadingState which sets state we'll overwrite)
        try
        {
            mediaSession?.SetMetadata(null);
        }
        catch
        {
            mediaSession?.SetMetadata(null);
        }

        // Set buffering state with no controls in one call (avoiding double SetPlaybackState)
        var playbackState = CreatePlaybackState(
            PlaybackStateCompat.StateBuffering,
            position: 0,
            actions: 0); // No actions - disables all controls

        if (playbackState != null && mediaSession != null)
        {
            mediaSession.SetPlaybackState(playbackState);
            mediaSession.Active = false;
        }
    }

    /// <summary>
    /// Sets playback state to STOPPED with only Play action available.
    /// Used when playback ends to show idle/ready state.
    /// </summary>
    public static void SetStoppedState(MediaSessionCompat mediaSession)
    {
        var playbackState = CreatePlaybackState(
            PlaybackStateCompat.StateStopped,
            position: 0,
            actions: PlaybackStateCompat.ActionPlay);

        if (playbackState != null)
        {
            mediaSession?.SetPlaybackState(playbackState);
        }
    }

    /// <summary>
    /// Updates only the playback state to BUFFERING while preserving everything else.
    /// Preserves existing metadata, actions (controls), position, and active status.
    /// Used when Play/PlayFromMediaId is called to show buffering progress bar only.
    /// </summary>
    public static void SetBufferingStateOnly(MediaSessionCompat mediaSession)
    {
        if (mediaSession == null)
        {
            return;
        }

        // Get current playback state to preserve actions and position
        var currentState = mediaSession.Controller?.PlaybackState;
        if (currentState != null)
        {
            // Preserve existing actions and position, only change state to buffering
            var playbackState = CreatePlaybackState(
                PlaybackStateCompat.StateBuffering,
                position: currentState.Position,
                playbackSpeed: currentState.PlaybackSpeed,
                actions: currentState.Actions);

            if (playbackState != null)
            {
                mediaSession.SetPlaybackState(playbackState);
            }
        }
        else
        {
            // If no current state exists, create a minimal buffering state with Play action
            var playbackState = CreatePlaybackState(
                PlaybackStateCompat.StateBuffering,
                position: 0,
                actions: PlaybackStateCompat.ActionPlay);

            if (playbackState != null)
            {
                mediaSession.SetPlaybackState(playbackState);
            }
        }
    }

    /// <summary>
    /// Creates a PlaybackStateCompat with the specified state, position, and actions.
    /// Centralizes all PlaybackStateCompat.Builder creation logic.
    /// </summary>
    public static PlaybackStateCompat? CreatePlaybackState(
        int state,
        long position = 0,
        float playbackSpeed = 1.0f,
        long actions = 0)
    {
        var builder = new PlaybackStateCompat.Builder();
        if (builder == null)
        {
            return null;
        }

        builder.SetActions(actions);
        builder.SetState(state, position, playbackSpeed, SystemClock.ElapsedRealtime());
        return builder.Build();
    }

    /// <summary>
    /// Creates a PlaybackStateCompat based on an existing playback state, updating position and actions.
    /// Used for position updates during playback.
    /// </summary>
    public static PlaybackStateCompat? CreatePlaybackStateFromExisting(
        PlaybackStateCompat existingState,
        long position,
        long actions)
    {
        return CreatePlaybackState(
            existingState.State,
            position,
            playbackSpeed: 1.0f,
            actions);
    }
}

