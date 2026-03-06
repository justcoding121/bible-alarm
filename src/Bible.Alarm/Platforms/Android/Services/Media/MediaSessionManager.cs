#nullable enable
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
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
    private readonly PlaybackStateManager playbackStateManager;
    private readonly MetadataManager metadataManager;

    public MediaSessionManager(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        // Initialize helper classes after serviceProvider is set
        initializer = new MediaSessionInitializer(logger, serviceProvider);
        playbackStateManager = new PlaybackStateManager();
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
        var actions = playbackStateManager.BuildPlaybackActions(canPlayNext, canPlayPrevious);

        var playbackState = playbackStateManager.CreatePlaybackState(
            state,
            position,
            playbackSpeed: 1.0f,
            actions);

        if (playbackState != null)
        {
            mediaSession?.SetPlaybackState(playbackState);
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
        if (playbackState == null || !playbackStateManager.IsPlaybackActive(playbackState))
        {
            return;
        }

        var positionMs = (long)position.TotalMilliseconds;
        var durationMs = (long)duration.TotalMilliseconds;
        var actions = playbackStateManager.BuildPlaybackActions(canPlayNext, canPlayPrevious);

        UpdatePlaybackStateWithPosition(playbackState, positionMs, actions);
        metadataManager.UpdateMetadataDuration(mediaSession, durationMs);
    }

    private void UpdatePlaybackStateWithPosition(PlaybackStateCompat playbackState, long positionMs, long actions)
    {
        var playbackStateCompat = playbackStateManager.CreatePlaybackStateFromExisting(
            playbackState,
            positionMs,
            actions);

        if (playbackStateCompat != null)
        {
            mediaSession?.SetPlaybackState(playbackStateCompat);
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
        mediaSession?.SetMetadata(metadata);
        // Reset tracked duration when metadata changes (new track may have different duration)
        metadataManager.LastDurationMs = null;
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
            playbackStateManager.SetBufferingStateOnly(mediaSession);
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

        playbackStateManager.SetStoppedState(mediaSession);
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

        var state = playbackStateManager.MapPlayStatusToState(status);

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
            UpdatePlaybackState(state, canPlayNext: canPlayNext, canPlayPrevious: canPlayPrevious);

            if (status == PlayStatus.Playing)
            {
                SetActive(true);
            }
            // Keep session active when paused (allows resume)
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
    /// Resets the tracked duration so the next position update will re-apply duration to metadata.
    /// Call after replacing metadata (e.g. HandlePlaybackMetadataChanged) to prevent the dedup
    /// check from skipping a duration update when the new metadata lost its duration value.
    /// </summary>
    public void ResetTrackedDuration()
    {
        metadataManager.LastDurationMs = null;
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

