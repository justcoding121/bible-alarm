#nullable enable
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using Bible.Alarm.Platforms.Android.Services.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.Scheduler.Models;
using Serilog;
using Application = Android.App.Application;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto;

/// <summary>
/// Singleton manager for the shared MediaSessionCompat instance.
/// Both Legacy and Modern Android Auto services must use the same MediaSessionCompat
/// to ensure seamless playback continuity and proper control handling.
/// </summary>
public sealed class MediaSessionManager
{
    private MediaSessionCompat? mediaSession;
    private static readonly ILogger logger = Log.ForContext<MediaSessionManager>();
    private readonly IServiceProvider serviceProvider;
    private long? lastDurationMs; // Track last duration to avoid unnecessary metadata updates

    public MediaSessionManager(IServiceProvider serviceProvider)
    {
        logger.Debug("MediaSessionManager constructor called with serviceProvider: {ServiceProvider}", serviceProvider != null ? "provided" : "null");
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Gets or creates the shared MediaSessionCompat instance.
    /// This is the single source of truth for media playback state across the entire app.
    /// Uses AndroidAutoMediaSessionHelper to create the session with thread-safe locking.
    /// Sets the callback after getting the session from the helper.
    /// </summary>
    public MediaSessionCompat GetOrCreate(bool isConnect = false)
    {
        // Get or create MediaSession from the global helper (thread-safe, prevents duplicates)
        if (mediaSession == null)
        {
            mediaSession = AndroidAutoMediaSessionHelper.Create();
            // Set callback after getting the session (requires IServiceProvider)
            SetMediaSessionCallback(mediaSession);
        }

        return mediaSession;
    }

    private void SetMediaSessionCallback(MediaSessionCompat session)
    {
        // CRITICAL: SetCallback must be called on the main thread (requires Looper)
        // Create MediaSessionCallback lazily to avoid startup dependency resolution issues
        var playbackService = serviceProvider.GetRequiredService<IPlaybackService>();
        var serviceLogger = serviceProvider.GetRequiredService<ILogger>();
        var mediaSessionCallback = new MediaSessionCallback(playbackService, serviceLogger);

        // Ensure SetCallback runs on main thread to avoid Looper exception
        if (MainThread.IsMainThread)
        {
            // The preferred path: if we are on the main thread, execute immediately.
            session.SetCallback(mediaSessionCallback);
            logger.Debug("MediaSessionCallback set synchronously on main thread");
        }
        else
        {
            // CRITICAL: Ensure SetCallback is run on the main thread without blocking the current Binder thread.
            // The callback will be set asynchronously. The MediaSession will be operational shortly after.
            // Android Auto is tolerant of this slight delay, and blocking the Binder thread causes deadlocks/ANRs.
            logger.Warning("MediaSession creation not on MainThread. Invoking SetCallback asynchronously to avoid blocking Binder thread.");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    session?.SetCallback(mediaSessionCallback);
                    logger.Information("Successfully set MediaSessionCallback on main thread (async)");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error setting MediaSessionCallback asynchronously");
                }
            });
            // NO BLOCKING CALL HERE (e.g., .Wait() or Task.Run().Wait())
            // The Binder thread returns immediately, allowing Android Auto to complete binding without timeout.
        }
    }



    /// <summary>
    /// Updates the playback state of the shared MediaSessionCompat.
    /// </summary>
    public void UpdatePlaybackState(int state, long position = 0, bool canPlayNext = false, bool canPlayPrevious = false)
    {
        var actions = BuildPlaybackActions(canPlayNext, canPlayPrevious);
        var playbackState = AndroidAutoPlayScreenHelper.CreatePlaybackState(
            state,
            position,
            playbackSpeed: 1.0f,
            actions);

        if (playbackState != null)
        {
            mediaSession?.SetPlaybackState(playbackState);
        }

        logger.Debug("UpdatePlaybackState completed for state: {State}, position: {Position}, actions: {Actions}",
            state, position, actions);
    }

    private static long BuildPlaybackActions(bool canPlayNext, bool canPlayPrevious)
    {
        // Base actions that are always available
        long actions = PlaybackStateCompat.ActionPlay |
                       PlaybackStateCompat.ActionPause |
                       PlaybackStateCompat.ActionPlayPause |
                       PlaybackStateCompat.ActionPlayFromMediaId;

        // Add next/previous actions only when available
        if (canPlayNext)
        {
            actions |= PlaybackStateCompat.ActionSkipToNext;
        }

        if (canPlayPrevious)
        {
            actions |= PlaybackStateCompat.ActionSkipToPrevious;
        }

        return actions;
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
        if (playbackState == null || !IsPlaybackActive(playbackState))
        {
            return;
        }

        var positionMs = (long)position.TotalMilliseconds;
        var durationMs = (long)duration.TotalMilliseconds;
        var actions = BuildPlaybackActions(canPlayNext, canPlayPrevious);

        UpdatePlaybackStateWithPosition(playbackState, positionMs, actions);
        UpdateMetadataDuration(durationMs);
    }

    private bool IsPlaybackActive(PlaybackStateCompat playbackState)
    {
        return playbackState.State is PlaybackStateCompat.StatePlaying or
               PlaybackStateCompat.StateBuffering or
               PlaybackStateCompat.StatePaused;
    }

    private void UpdatePlaybackStateWithPosition(PlaybackStateCompat playbackState, long positionMs, long actions)
    {
        var playbackStateCompat = AndroidAutoPlayScreenHelper.CreatePlaybackStateFromExisting(
            playbackState,
            positionMs,
            actions);

        if (playbackStateCompat != null)
        {
            mediaSession?.SetPlaybackState(playbackStateCompat);
        }
    }

    private void UpdateMetadataDuration(long durationMs)
    {
        // Only update duration if it has changed (duration rarely changes, only on track change)
        // Position updates are frequent (~200ms), but duration only changes when a new track starts
        if (durationMs <= 0 || durationMs == lastDurationMs)
        {
            return;
        }

        if (mediaSession?.Controller?.Metadata == null)
        {
            return;
        }

        // Use CreateMetadataBuilderFromExisting to ensure artwork and all metadata is preserved
        var existingMetadata = mediaSession.Controller.Metadata;
        if (existingMetadata != null)
        {
            var metadataBuilder = AndroidAutoPlayScreenHelper.CreateMetadataBuilderFromExisting(existingMetadata);
            metadataBuilder.PutLong(MediaMetadataCompat.MetadataKeyDuration, durationMs);
            var metadata = metadataBuilder.Build();
            if (metadata != null)
            {
                mediaSession?.SetMetadata(metadata);
                lastDurationMs = durationMs;
                logger.Debug("Duration updated in metadata - Duration: {Duration}ms (artwork preserved)", durationMs);
            }
        }
    }

    /// <summary>
    /// Updates the metadata of the shared MediaSessionCompat.
    /// Preserves existing artwork and MediaId if present to prevent them from disappearing.
    /// </summary>
    public void UpdateMetadata(string title, string artist, string? album = null, int? scheduleId = null, string? artworkUrl = null)
    {
        logger.Debug("UpdateMetadata called with title: {Title}, artist: {Artist}, album: {Album}, scheduleId: {ScheduleId}, artworkUrl: {ArtworkUrl}",
            title, artist, album ?? "null", scheduleId?.ToString() ?? "null", artworkUrl ?? "null");

        if (mediaSession == null)
        {
            logger.Warning("MediaSessionCompat is null, cannot update metadata. Call GetOrCreate() first.");
            return;
        }

        var builder = CreateMetadataBuilder(title, artist, album);
        if (builder == null)
        {
            logger.Warning("Failed to create MediaMetadataCompat.Builder");
            return;
        }

        PreserveExistingMetadata(builder, scheduleId, artworkUrl);
        ApplyMetadata(builder);
    }

    private MediaMetadataCompat.Builder? CreateMetadataBuilder(string title, string artist, string? album)
    {
        // Handle empty strings with fallback values (consistent with AndroidAutoPlayScreenHelper)
        // This ensures we never show empty text in Android Auto UI
        return new MediaMetadataCompat.Builder()
            ?.PutString(MediaMetadataCompat.MetadataKeyTitle, string.IsNullOrEmpty(title) ? "Bible Alarm" : title)
            ?.PutString(MediaMetadataCompat.MetadataKeyArtist, string.IsNullOrEmpty(artist) ? "Tap to play" : artist)
            ?.PutString(MediaMetadataCompat.MetadataKeyAlbum, string.IsNullOrEmpty(album) ? "..." : album);
    }

    private void PreserveExistingMetadata(MediaMetadataCompat.Builder builder, int? scheduleId, string? artworkUrl)
    {
        if (mediaSession?.Controller?.Metadata != null)
        {
            var existingMetadata = mediaSession.Controller.Metadata;
            PreserveMediaId(builder, existingMetadata, scheduleId);
            PreserveOrLoadArtwork(builder, existingMetadata, artworkUrl);
        }
        else
        {
            if (scheduleId.HasValue)
            {
                // Set MediaId if no existing metadata and scheduleId is provided
                builder?.PutString(MediaMetadataCompat.MetadataKeyMediaId, scheduleId.Value.ToString());
            }

            // Load artwork from URL if provided and no existing metadata
            if (builder != null)
            {
                LoadArtworkFromUrl(builder, artworkUrl);
            }
        }
    }

    private static void PreserveMediaId(MediaMetadataCompat.Builder builder, MediaMetadataCompat? existingMetadata, int? scheduleId)
    {
        // Preserve MediaId (scheduleId) for OnPlayFromMediaId
        var existingMediaId = existingMetadata?.GetString(MediaMetadataCompat.MetadataKeyMediaId);
        if (!string.IsNullOrEmpty(existingMediaId))
        {
            builder?.PutString(MediaMetadataCompat.MetadataKeyMediaId, existingMediaId);
        }
        else if (scheduleId.HasValue)
        {
            // Set MediaId if provided and not already present
            builder?.PutString(MediaMetadataCompat.MetadataKeyMediaId, scheduleId.Value.ToString());
        }
    }

    private void PreserveOrLoadArtwork(MediaMetadataCompat.Builder builder, MediaMetadataCompat? existingMetadata, string? artworkUrl)
    {
        // Always use new artworkUrl if provided (e.g., when switching from playback to default schedule)
        // This ensures artwork is updated correctly when metadata changes
        if (!string.IsNullOrEmpty(artworkUrl))
        {
            // Load artwork from URL - this will replace any existing artwork
            LoadArtworkFromUrl(builder, artworkUrl);
        }
        else
        {
            // Only preserve existing artwork if no new artworkUrl is provided
            Bitmap? existingArtwork = existingMetadata?.GetBitmap(MediaMetadataCompat.MetadataKeyArt);
            if (existingArtwork != null)
            {
                builder?.PutBitmap(MediaMetadataCompat.MetadataKeyArt, existingArtwork);
            }
        }
    }

    private void LoadArtworkFromUrl(MediaMetadataCompat.Builder builder, string? artworkUrl)
    {
        if (string.IsNullOrEmpty(artworkUrl))
        {
            return;
        }

        try
        {
            var artworkService = serviceProvider.GetService<AndroidArtworkService>();
            if (artworkService != null)
            {
                var artworkBitmap = artworkService.LoadArtworkBitmap(artworkUrl);
                if (artworkBitmap != null)
                {
                    builder?.PutBitmap(MediaMetadataCompat.MetadataKeyArt, artworkBitmap);
                    logger.Debug("Loaded artwork bitmap from: {ArtworkUrl}", artworkUrl);
                }
                else
                {
                    logger.Debug("Failed to load artwork bitmap from: {ArtworkUrl}", artworkUrl);
                }
            }
            else
            {
                logger.Warning("AndroidArtworkService not available - cannot load artwork");
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error loading artwork bitmap from: {ArtworkUrl}", artworkUrl);
        }
    }

    private void ApplyMetadata(MediaMetadataCompat.Builder builder)
    {
        var metadata = builder?.Build();
        mediaSession?.SetMetadata(metadata);
        // Reset tracked duration when metadata changes (new track may have different duration)
        lastDurationMs = null;
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
            AndroidAutoPlayScreenHelper.SetBufferingStateOnly(mediaSession);
            logger.Debug("Set MediaSession to buffering state only (preserving metadata and controls)");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error setting buffering state only");
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

        AndroidAutoPlayScreenHelper.SetStoppedState(mediaSession);
    }

    /// <summary>
    /// Sets the playback status, updating MediaSessionCompat active state and audio focus accordingly.
    /// </summary>
    public void SetPlaybackStatus(PlayStatus status, bool canPlayNext = false, bool canPlayPrevious = false)
    {
        if (mediaSession == null)
        {
            logger.Warning("MediaSessionCompat is null, cannot set playback status. Call GetOrCreate() first.");
            return;
        }

        var state = status switch
        {
            PlayStatus.Playing => PlaybackStateCompat.StatePlaying,
            PlayStatus.Paused => PlaybackStateCompat.StatePaused,
            PlayStatus.Loading => PlaybackStateCompat.StateBuffering,
            PlayStatus.Stopped => PlaybackStateCompat.StateStopped,
            PlayStatus.Ended => PlaybackStateCompat.StateStopped,
            PlayStatus.Failed => PlaybackStateCompat.StateError,
            _ => PlaybackStateCompat.StateNone
        };

        if (status is PlayStatus.Stopped or PlayStatus.Ended)
        {
            // When stopped/ended, update playback state to stopped
            // Metadata for next schedule is handled by state-based approach:
            // PlaybackService.ResetAsync() dispatches SetCarPlayScreenAction
            // which triggers DefaultCarScreenEffect -> SetDefaultScheduleMetadataAction
            // which is handled by MediaSessionEffect.HandleSetDefaultScheduleMetadata()
            UpdatePlaybackStateForStop();
            SetActive(false);
            // Reset tracked duration when playback stops
            lastDurationMs = null;
            // Note: Audio focus is released globally by AudioFocusEffect when playback stops
            logger.Information("MediaSessionCompat set to stopped/inactive - next schedule metadata will be updated via state");
        }
        else
        {
            // Update playback state normally for other states with navigation availability
            UpdatePlaybackState(state, canPlayNext: canPlayNext, canPlayPrevious: canPlayPrevious);

            // Set MediaSessionCompat active when playing - this is critical for Android Auto audio routing
            // Note: Audio focus is managed globally by AudioFocusEffect, not here
            if (status == PlayStatus.Playing)
            {
                SetActive(true);
                logger.Debug("MediaSessionCompat set to active (playing) - Android Auto can now route audio");
            }
            // Keep session active when paused (allows resume)
        }
    }


    internal void SetActive(bool active)
    {
        if (mediaSession == null)
        {
            logger.Warning("MediaSessionCompat is null, cannot set active state. Call GetOrCreate() first.");
            return;
        }
        mediaSession.Active = active;
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

