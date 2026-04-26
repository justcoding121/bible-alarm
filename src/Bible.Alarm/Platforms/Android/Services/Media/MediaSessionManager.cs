#nullable enable
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using Bible.Alarm.Platforms.Android.Services.AndroidAuto;
using Bible.Alarm.Platforms.Android.Services.Media.Interfaces;
using Bible.Alarm.Platforms.Android.Services.Media.MediaSessionManagerHelpers;
using Bible.Alarm.Services.Media.Models;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Media;

/// <summary>
/// Singleton manager for the shared MediaSessionCompat instance.
/// Both Legacy and Modern Android Auto services must use the same MediaSessionCompat
/// to ensure seamless playback continuity and proper control handling.
/// </summary>
public sealed class MediaSessionManager : IMediaSessionManager
{
    private MediaSessionCompat? mediaSession;
    private static readonly ILogger logger = Log.ForContext<MediaSessionManager>();
    private readonly IServiceProvider serviceProvider;

    // Helper classes - initialized in constructor
    private readonly MediaSessionInitializer initializer;
    private readonly MetadataManager metadataManager;

    // Dedup tracking: skip redundant SetPlaybackState calls that cause
    // visual flickering (title/subtitle bounce) on the Android Auto Now Playing screen.
    private int? lastSetState;
    private long? lastSetPosition;

    public MediaSessionManager(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        // Initialize helper classes after serviceProvider is set
        initializer = new MediaSessionInitializer(logger, serviceProvider);
        metadataManager = new MetadataManager(logger, serviceProvider);
    }

    /// <summary>
    /// Gets or creates the shared MediaSessionCompat instance.
    /// This is the single source of truth for media playback state across the entire app.
    /// Uses MediaSessionHelper to create the session with thread-safe locking.
    /// Sets the callback after getting the session from the helper.
    /// </summary>
    public MediaSessionCompat GetOrCreate(bool isConnect = false)
    {
        // Get or create MediaSession from the global helper (thread-safe, prevents duplicates)
        if (mediaSession == null)
        {
            mediaSession = MediaSessionHelper.Create();
            // Set callback after getting the session (requires IServiceProvider)
            initializer.SetMediaSessionCallback(mediaSession);
        }

        return mediaSession;
    }



    /// <summary>
    /// Updates the playback state of the shared MediaSessionCompat.
    /// </summary>
    public void UpdatePlaybackState(int state, long position = 0, bool canPlayNext = false, bool canPlayPrevious = false)
    {
        if (state == lastSetState && position == lastSetPosition)
        {
            return;
        }

        var actions = PlaybackStateManager.BuildPlaybackActions(canPlayNext, canPlayPrevious);

        var playbackState = PlaybackStateManager.CreatePlaybackState(
            state,
            position,
            playbackSpeed: 1.0f,
            actions);

        if (playbackState != null)
        {
            mediaSession?.SetPlaybackState(playbackState);
            lastSetState = state;
            lastSetPosition = position;
        }
        else
        {
            logger.Warning("[AndroidAuto] Failed to create PlaybackStateCompat - state update skipped");
        }
    }

    /// <summary>
    /// Updates the playback position for the progress bar in Android Auto.
    /// Should be called regularly during playback to keep the progress bar updated.
    /// </summary>
    public void UpdatePlaybackPosition(TimeSpan position, TimeSpan duration, bool canPlayNext = false, bool canPlayPrevious = false)
    {
        if (mediaSession == null)
        {
            return;
        }

        var playbackState = mediaSession.Controller?.PlaybackState;
        if (playbackState == null || !PlaybackStateManager.IsPlaybackActive(playbackState))
        {
            return;
        }

        var positionMs = (long)position.TotalMilliseconds;
        var durationMs = (long)duration.TotalMilliseconds;
        var actions = PlaybackStateManager.BuildPlaybackActions(canPlayNext, canPlayPrevious);

        UpdatePlaybackStateWithPosition(playbackState, positionMs, actions);
        metadataManager.UpdateMetadataDuration(mediaSession, durationMs);
    }

    private void UpdatePlaybackStateWithPosition(PlaybackStateCompat playbackState, long positionMs, long actions)
    {
        // Use the last intentionally-set state code rather than the Controller snapshot.
        // The Controller snapshot can be stale (read before a concurrent state change
        // from another thread), and writing back the stale state would revert the
        // intentional change (e.g., revert Playing back to Buffering).
        var stateCode = lastSetState ?? playbackState.State;

        if (stateCode == lastSetState && positionMs == lastSetPosition)
        {
            return;
        }

        var playbackStateCompat = PlaybackStateManager.CreatePlaybackState(
            stateCode,
            positionMs,
            playbackSpeed: 1.0f,
            actions);

        if (playbackStateCompat != null)
        {
            mediaSession?.SetPlaybackState(playbackStateCompat);
            lastSetState = stateCode;
            lastSetPosition = positionMs;
        }
        else
        {
            logger.Warning("[AndroidAuto] UpdatePlaybackStateWithPosition: Failed to create PlaybackStateCompat - state update skipped");
        }
    }

    /// <summary>
    /// Updates the metadata of the shared MediaSessionCompat.
    /// Preserves existing artwork and MediaId if present to prevent them from disappearing.
    /// </summary>
    public void UpdateMetadata(string title, string artist, string? album = null, int? scheduleId = null, string? artworkUrl = null)
    {
        if (mediaSession == null)
        {
            logger.Warning("MediaSessionCompat is null, cannot update metadata. Call GetOrCreate() first.");
            return;
        }

        var builder = metadataManager.CreateMetadataBuilder(title, artist, album);
        if (builder == null)
        {
            logger.Warning("Failed to create MediaMetadataCompat.Builder");
            return;
        }

        metadataManager.PreserveExistingMetadata(builder, mediaSession, scheduleId, artworkUrl);
        ApplyMetadata(builder);
    }

    private void ApplyMetadata(MediaMetadataCompat.Builder builder)
    {
        var metadata = builder?.Build();
        if (metadata == null)
        {
            return;
        }

        var currentMetadata = mediaSession?.Controller?.Metadata;
        if (ShouldSkipRedundantMetadataUpdate(metadata, currentMetadata))
        {
            return;
        }

        mediaSession?.SetMetadata(metadata);
        // Track the duration that was just set so subsequent position updates with the
        // same duration get deduped (avoid redundant SetMetadata calls that cause time bounce).
        var durationMs = metadata.GetLong(MediaMetadataCompat.MetadataKeyDuration);
        metadataManager.LastDurationMs = durationMs > 0 ? durationMs : null;
    }

    /// <summary>
    /// Returns true when the new metadata would not change anything meaningful on the session
    /// (same title/artist and no meaningful artwork delta), so SetMetadata can be skipped.
    /// </summary>
    private bool ShouldSkipRedundantMetadataUpdate(MediaMetadataCompat metadata, MediaMetadataCompat? currentMetadata)
    {
        if (currentMetadata == null)
        {
            return false;
        }

        var newTitle = metadata.GetString(MediaMetadataCompat.MetadataKeyTitle);
        var newArtist = metadata.GetString(MediaMetadataCompat.MetadataKeyArtist);
        var currentTitle = currentMetadata.GetString(MediaMetadataCompat.MetadataKeyTitle);
        var currentArtist = currentMetadata.GetString(MediaMetadataCompat.MetadataKeyArtist);
        if (newTitle != currentTitle || newArtist != currentArtist)
        {
            return false;
        }

        var currentArtwork = currentMetadata.GetBitmap(MediaMetadataCompat.MetadataKeyArt);
        var newArtwork = metadata.GetBitmap(MediaMetadataCompat.MetadataKeyArt);

        var shouldApplyArtworkChange =
            (currentArtwork == null && newArtwork != null)
            || (currentArtwork != null && newArtwork == null);

        if (shouldApplyArtworkChange)
        {
            if (currentArtwork == null && newArtwork != null)
            {
                logger.Debug("[AndroidAuto] Title/Artist unchanged but artwork missing from current metadata — applying update with artwork");
            }
            else
            {
                logger.Debug("[AndroidAuto] Title/Artist unchanged but clearing artwork in metadata (e.g. removed app icon fallback)");
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// Sets buffering state while preserving existing metadata and controls.
    /// Used when starting fresh playback from Android Auto to avoid navigation away from Now Playing screen.
    /// Only updates the playback state to buffering, preserving everything else.
    /// </summary>
    public void SetBufferingStateOnly()
    {
        if (mediaSession == null)
        {
            logger.Warning("MediaSessionCompat is null, cannot set buffering state. Call GetOrCreate() first.");
            return;
        }

        try
        {
            PlaybackStateManager.SetBufferingStateOnly(mediaSession);
            lastSetState = PlaybackStateCompat.StateBuffering;
            lastSetPosition = mediaSession.Controller?.PlaybackState?.Position;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[AndroidAuto] Error setting buffering state only");
        }
    }

    /// <summary>
    /// Updates playback state to STOPPED with only Play action available.
    /// This removes playback controls from Android Auto screen when playback ends.
    /// </summary>
    public void UpdatePlaybackStateForStop()
    {
        if (mediaSession == null)
        {
            logger.Warning("MediaSessionCompat is null, cannot update playback state. Call GetOrCreate() first.");
            return;
        }

        PlaybackStateManager.SetStoppedState(mediaSession);
        // SetStoppedState uses StatePaused with position 0
        lastSetState = PlaybackStateCompat.StatePaused;
        lastSetPosition = 0;
    }

    /// <summary>
    /// Sets the playback status, updating MediaSessionCompat active state and audio focus accordingly.
    /// </summary>
    public void SetPlaybackStatus(PlayStatus status, bool canPlayNext = false, bool canPlayPrevious = false)
    {
        if (mediaSession == null)
        {
            logger.Warning("[AndroidAuto] MediaSessionCompat is null, cannot set playback status. Call GetOrCreate() first.");
            return;
        }

        var state = PlaybackStateManager.MapPlayStatusToState(status);

        if (status is PlayStatus.Stopped or PlayStatus.Ended)
        {
            // When stopped/ended, update playback state to stopped
            // Metadata for next schedule is handled by state-based approach:
            // PlaybackService.ResetAsync() dispatches SetCarPlayScreenAction
            // which triggers DefaultCarScreenEffect -> SetDefaultScheduleMetadataAction
            // which is handled by MediaSessionEffect.HandleSetDefaultScheduleMetadata()
            UpdatePlaybackStateForStop();
            // CRITICAL: Keep MediaSession active even when stopped so Android Auto can discover it
            SetActive(true);
            metadataManager.LastDurationMs = null;
        }
        else
        {
            // Preserve current position from the MediaSession to avoid resetting
            // the progress bar to 0:00 on state transitions (especially pause).
            // For Playing state, Android Auto uses position + playbackSpeed + timestamp
            // to advance the position automatically.
            var position = mediaSession?.Controller?.PlaybackState?.Position ?? 0;
            UpdatePlaybackState(state, position: position, canPlayNext: canPlayNext, canPlayPrevious: canPlayPrevious);

            if (status == PlayStatus.Playing)
            {
                SetActive(true);
            }
            // Keep session active when paused (allows resume)
        }
    }


    /// <summary>
    /// Sets the MediaSession to error state with a user-facing error message.
    /// Uses setErrorMessage so Android Auto displays the error on the Now Playing screen
    /// instead of navigating away to the browse tree with a generic error.
    /// </summary>
    public void SetErrorState(string? errorMessage, bool canPlayNext = false, bool canPlayPrevious = false)
    {
        if (mediaSession == null)
        {
            logger.Warning("[AndroidAuto] MediaSessionCompat is null, cannot set error state. Call GetOrCreate() first.");
            return;
        }

        var actions = PlaybackStateManager.BuildPlaybackActions(canPlayNext, canPlayPrevious);
        var position = mediaSession?.Controller?.PlaybackState?.Position ?? 0;
        var message = string.IsNullOrEmpty(errorMessage) ? "Playback failed. Tap Play to retry." : errorMessage;

        var playbackState = AndroidAutoPlayScreenHelper.CreateErrorPlaybackState(position, actions, message);
        if (playbackState != null)
        {
            mediaSession?.SetPlaybackState(playbackState);
            lastSetState = PlaybackStateCompat.StateError;
            lastSetPosition = position;
        }
        else
        {
            logger.Warning("[AndroidAuto] Failed to create error PlaybackStateCompat");
        }
    }

    public void SetActive(bool active)
    {
        if (mediaSession == null)
        {
            logger.Warning("[AndroidAuto] MediaSessionCompat is null, cannot set active state. Call GetOrCreate() first.");
            return;
        }
        mediaSession.Active = active;
    }


    /// <summary>
    /// Updates the duration in metadata via the deduped MetadataManager path.
    /// Prevents duplicate SetMetadata calls when both HandlePlaybackDurationChanged
    /// and position updates try to set the same duration.
    /// </summary>
    public void UpdateDuration(TimeSpan duration)
    {
        if (mediaSession == null)
        {
            return;
        }

        metadataManager.UpdateMetadataDuration(mediaSession, (long)duration.TotalMilliseconds);
    }

    /// <summary>
    /// Tracks the duration that was just set in metadata so subsequent position updates
    /// with the same duration value get deduped (skip redundant SetMetadata calls).
    /// Call after directly setting metadata that includes a duration value.
    /// </summary>
    public void SetTrackedDuration(long durationMs)
    {
        metadataManager.LastDurationMs = durationMs > 0 ? durationMs : null;
    }

    /// <summary>
    /// Gets the SessionToken from the shared MediaSessionCompat.
    /// This token is used by both Legacy and Modern Android Auto services.
    /// </summary>
    public MediaSessionCompat.Token? Token
    {
        get
        {
            return mediaSession?.SessionToken;
        }
    }

}
