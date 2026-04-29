#nullable enable
using _Microsoft.Android.Resource.Designer;
using Android.Graphics;
using Android.OS;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using AndroidX.Core.Content;
using Bible.Alarm.Shared.Constants;
using Serilog;

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
    /// Creates an error PlaybackStateCompat with a user-facing error message.
    /// Android Auto shows the message on the Now Playing screen instead of
    /// navigating away to the browse tree with a generic error.
    /// </summary>
    public static PlaybackStateCompat? CreateErrorPlaybackState(
        long position,
        long actions,
        string errorMessage)
    {
        var builder = new PlaybackStateCompat.Builder();
        builder.SetActions(actions);
        builder.SetState(PlaybackStateCompat.StateError, position, 0f, SystemClock.ElapsedRealtime());
        builder.SetErrorMessage(PlaybackStateCompat.ErrorCodeAppError, errorMessage);
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
        builder.PutString(MediaMetadataCompat.MetadataKeyTitle, string.IsNullOrEmpty(title) ? AppConstants.AppSettings.ApplicationDisplayName : title);
        builder.PutString(MediaMetadataCompat.MetadataKeyArtist, string.IsNullOrEmpty(artist) ? AppConstants.Media.NowPlayingPlaceholder.ArtistTapToPlay : artist);
        builder.PutString(MediaMetadataCompat.MetadataKeyAlbum, string.IsNullOrEmpty(album) ? AppConstants.Media.NowPlayingPlaceholder.AlbumEllipsis : album);
        // Always include duration key so the time area is always allocated on the Now Playing screen.
        // Prevents layout bounce (title/subtitle shifting) when time appears/disappears during transitions.
        builder.PutLong(MediaMetadataCompat.MetadataKeyDuration, 0);
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
                builder.PutString(MediaMetadataCompat.MetadataKeyTitle, AppConstants.AppSettings.ApplicationDisplayName);
            }
            if (string.IsNullOrEmpty(artist))
            {
                builder.PutString(MediaMetadataCompat.MetadataKeyArtist, AppConstants.Media.NowPlayingPlaceholder.ArtistTapToPlay);
            }
            if (string.IsNullOrEmpty(album))
            {
                builder.PutString(MediaMetadataCompat.MetadataKeyAlbum, AppConstants.Media.NowPlayingPlaceholder.AlbumEllipsis);
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

    private static readonly ILogger logger = Log.ForContext(typeof(AndroidAutoPlayScreenHelper));
    private static Bitmap? cachedAppIconBitmap;

    /// <summary>
    /// Returns the app icon bitmap, loading and caching it on first call.
    /// Used as fallback artwork when no track-specific artwork is available.
    /// </summary>
    public static Bitmap? GetOrLoadAppIconBitmap()
    {
        if (cachedAppIconBitmap != null)
        {
            return cachedAppIconBitmap;
        }

        try
        {
            var context = global::Android.App.Application.Context;
            var drawable = ContextCompat.GetDrawable(
                context, ResourceConstant.Drawable.ic_launcher_round);
            if (drawable == null)
            {
                return null;
            }

            var width = drawable.IntrinsicWidth > 0 ? drawable.IntrinsicWidth : 512;
            var height = drawable.IntrinsicHeight > 0 ? drawable.IntrinsicHeight : 512;
            var bitmap = Bitmap.CreateBitmap(
                width, height, Bitmap.Config.Argb8888!);
            if (bitmap == null)
            {
                return null;
            }

            var canvas = new Canvas(bitmap);
            drawable.SetBounds(0, 0, width, height);
            drawable.Draw(canvas);
            cachedAppIconBitmap = bitmap;
            return bitmap;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.AndroidAutoPlayScreenDiagnosticsLog.ErrorLoadingAppIconFallbackArtwork);
            return null;
        }
    }
}

