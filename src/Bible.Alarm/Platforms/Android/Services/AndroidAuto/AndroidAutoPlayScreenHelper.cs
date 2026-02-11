#nullable enable
using Android.OS;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto;

/// <summary>
/// Helper for managing Android Auto play screen UI states and playback states.
/// Intentionally does NOT depend on MAUI DI/services.
/// Centralizes all PlaybackStateCompat.Builder and MediaMetadataCompat.Builder creation logic.
/// </summary>
public static class AndroidAutoPlayScreenHelper
{
    /// <summary>
    /// Applies blank loading state (buffering with progress bar animation).
    /// Optionally clears metadata and sets STATE_BUFFERING to indicate player is initializing.
    /// Follows standard Media Resumption patterns (like YouTube Music).
    /// </summary>
    /// <param name="mediaSession">The MediaSessionCompat instance</param>
    public static void ApplyBlankLoadingState(MediaSessionCompat mediaSession)
    {
        // Clear metadata (no artwork, no title/subtitle) + set buffering state.
        // Following standard Media Resumption patterns (like YouTube Music):
        // - Use STATE_BUFFERING instead of STATE_NONE to indicate player is initializing
        // - This tells Android Auto that the app is responsive and ready, avoiding blank screens
        // - Keep session active so Android Auto can see the session

        try
        {
            // Clear metadata - no artwork, no text
            mediaSession?.SetMetadata(null);
        }
        catch
        {
            mediaSession?.SetMetadata(null);
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
    /// Sets playback state to PAUSED with Play and Pause actions available.
    /// Used when metadata is available but playback hasn't started yet.
    /// StatePaused (instead of StateStopped) indicates the media is "ready" and helps Android Auto show the split view.
    /// </summary>
    public static void SetStoppedState(MediaSessionCompat mediaSession)
    {
        // Use StatePaused instead of StateStopped when metadata is available
        // This indicates the media is "ready" rather than "stopped", which helps Android Auto show the split view
        // Include both Play and Pause actions to indicate the media is ready to play
        var playbackState = CreatePlaybackState(
            PlaybackStateCompat.StatePaused,
            position: 0,
            actions: PlaybackStateCompat.ActionPlay | PlaybackStateCompat.ActionPause | PlaybackStateCompat.ActionPlayPause);

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

    /// <summary>
    /// Creates a new MediaMetadataCompat.Builder with basic metadata (title, artist, album).
    /// Uses standard fallback values from DefaultScheduleService when values are null or empty.
    /// </summary>
    public static MediaMetadataCompat.Builder CreateMetadataBuilder(string? title, string? artist, string? album = null)
    {
        var builder = new MediaMetadataCompat.Builder();
        builder.PutString(MediaMetadataCompat.MetadataKeyTitle, string.IsNullOrEmpty(title) ? "Bible Alarm" : title);
        builder.PutString(MediaMetadataCompat.MetadataKeyArtist, string.IsNullOrEmpty(artist) ? "Tap to play" : artist);
        builder.PutString(MediaMetadataCompat.MetadataKeyAlbum, string.IsNullOrEmpty(album) ? "..." : album);
        return builder;
    }

    /// <summary>
    /// Creates a MediaMetadataCompat.Builder from existing metadata, preserving all existing values.
    /// Used for updating specific fields while keeping others intact.
    /// Handles empty strings in existing metadata by replacing them with fallback values.
    /// </summary>
    public static MediaMetadataCompat.Builder CreateMetadataBuilderFromExisting(MediaMetadataCompat? existingMetadata)
    {
        if (existingMetadata != null)
        {
            var builder = new MediaMetadataCompat.Builder(existingMetadata);
            // Ensure empty strings are replaced with fallback values
            var title = existingMetadata.GetString(MediaMetadataCompat.MetadataKeyTitle);
            var artist = existingMetadata.GetString(MediaMetadataCompat.MetadataKeyArtist);
            var album = existingMetadata.GetString(MediaMetadataCompat.MetadataKeyAlbum);

            if (string.IsNullOrEmpty(title))
            {
                builder.PutString(MediaMetadataCompat.MetadataKeyTitle, "Bible Alarm");
            }
            if (string.IsNullOrEmpty(artist))
            {
                builder.PutString(MediaMetadataCompat.MetadataKeyArtist, "Tap to play");
            }
            if (string.IsNullOrEmpty(album))
            {
                builder.PutString(MediaMetadataCompat.MetadataKeyAlbum, "...");
            }

            // Explicitly preserve artwork bitmap to ensure it's not lost
            // The constructor should copy it, but explicitly preserving ensures notification displays it
            var existingArtwork = existingMetadata.GetBitmap(MediaMetadataCompat.MetadataKeyArt);
            if (existingArtwork != null)
            {
                builder.PutBitmap(MediaMetadataCompat.MetadataKeyArt, existingArtwork);
            }

            return builder;
        }
        // If no existing metadata, create a new builder with fallback values
        return CreateMetadataBuilder(null, null, null);
    }

    /// <summary>
    /// Creates a MediaMetadataCompat.Builder with basic metadata and MediaId (scheduleId).
    /// </summary>
    public static MediaMetadataCompat.Builder CreateMetadataBuilderWithMediaId(
        string? title,
        string? artist,
        string? album = null,
        int? scheduleId = null)
    {
        var builder = CreateMetadataBuilder(title, artist, album);
        if (scheduleId.HasValue)
        {
            builder.PutString(MediaMetadataCompat.MetadataKeyMediaId, scheduleId.Value.ToString());
        }
        return builder;
    }
}

