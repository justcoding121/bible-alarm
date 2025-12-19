#nullable enable
using Android.Content;
using Android.OS;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto;

/// <summary>
/// Singleton manager for the shared MediaSessionCompat instance.
/// Both Legacy and Modern Android Auto services must use the same MediaSessionCompat
/// to ensure seamless playback continuity and proper control handling.
/// </summary>
public sealed class MediaSessionManager
{
    private MediaSessionCompat? mediaSession;
    private readonly object lockObject = new();
    private static readonly ILogger logger = Log.ForContext<MediaSessionManager>();
    private readonly IServiceProvider serviceProvider;

    public MediaSessionManager(IServiceProvider serviceProvider)
    {
        logger.Debug("MediaSessionManager constructor called with serviceProvider: {ServiceProvider}", serviceProvider != null ? "provided" : "null");
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Gets or creates the shared MediaSessionCompat instance.
    /// This is the single source of truth for media playback state across the entire app.
    /// Starts in stopped/inactive state. The MediaSessionEffect will update it when playback status changes.
    /// </summary>
    public MediaSessionCompat GetOrCreate(bool isConnect = false)
    {
        // Fast path: if already created, return it
        if (mediaSession == null)
        {
            // Double-checked locking pattern for thread safety
            lock (lockObject)
            {
                // Check again inside lock (another thread might have created it)
                if (mediaSession == null)
                {
                    // Create the MediaSessionCompat instance
                    var context = global::Android.App.Application.Context;
                    if (context == null)
                    {
                        logger.Error("Android Application.Context is null - cannot create MediaSessionCompat");
                        throw new InvalidOperationException("Android Application.Context is null");
                    }

                    logger.Information("Creating shared MediaSessionCompat instance");

                    // Create ComponentName for MediaButtonReceiver to suppress warning
                    // We handle media buttons programmatically in LegacyMediaBrowserService.OnStartCommand
                    // Using the service class name to register as media button receiver
                    var componentName = new ComponentName(context, "bible.alarm.platforms.android.services.androidauto.LegacyMediaBrowserService");

                    mediaSession = new MediaSessionCompat(context, "BibleAlarmSession", componentName, null);
                    if (mediaSession == null)
                    {
                        logger.Error("Failed to create MediaSessionCompat instance");
                        throw new InvalidOperationException("Failed to create MediaSessionCompat instance");
                    }

#pragma warning disable CS0618 // Obsolete API from AndroidX library - deprecated but still functional
                    mediaSession.SetFlags(
                        MediaSessionCompat.FlagHandlesMediaButtons |
                        MediaSessionCompat.FlagHandlesTransportControls);
#pragma warning restore CS0618

                    // CRITICAL: SetCallback must be called on the main thread (requires Looper)
                    // Create MediaSessionCallback lazily to avoid startup dependency resolution issues
                    var playbackService = serviceProvider.GetRequiredService<IPlaybackService>();
                    var serviceLogger = serviceProvider.GetRequiredService<ILogger>();
                    var mediaSessionCallback = new MediaSessionCallback(playbackService, serviceLogger);

                    // Ensure SetCallback runs on main thread to avoid Looper exception
                    if (MainThread.IsMainThread)
                    {
                        // The preferred path: if we are on the main thread, execute immediately.
                        mediaSession.SetCallback(mediaSessionCallback);
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
                                mediaSession?.SetCallback(mediaSessionCallback);
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

                    // Start with stopped state — prevents auto-play on bind
                    // The effect will update this when it receives playback status changes.
                    //
                    // IMPORTANT for Android Auto UX:
                    // Do NOT start in Stopped/Paused if we are not ready to show the player UI yet.
                    // Start in a neutral, non-interactive state (STATE_NONE + actions=0).
                    //
                    // Immediately present a blank, non-interactive loading UI for Android Auto.
                    // This does not depend on MAUI bootstrap; it only needs the MediaSession itself.
                    try
                    {
                        AndroidAutoLoadingUiHelper.ApplyBlankLoadingState(mediaSession, context);
                    }
                    catch (Exception ex)
                    {
                        logger.Warning(ex, "Failed to apply Android Auto blank loading state");
                    }

                    // Verify SessionToken is available
                    if (mediaSession.SessionToken == null)
                    {
                        logger.Error("MediaSessionCompat.SessionToken is null after creation - this should not happen");
                        throw new InvalidOperationException("MediaSessionCompat.SessionToken is null after creation");
                    }

                    logger.Information("MediaSessionCompat created successfully. Initial state: Stopped, Active: False, SessionToken available: {HasToken}",
                        mediaSession.SessionToken != null);
                }
            }
        }

        return mediaSession;
    }


    /// <summary>
    /// Updates the playback state of the shared MediaSessionCompat.
    /// </summary>
    public void UpdatePlaybackState(int state, long position = 0, bool canPlayNext = false, bool canPlayPrevious = false)
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

        var builder = new PlaybackStateCompat.Builder();
        if (builder != null)
        {
            builder.SetActions(actions);
            builder.SetState(state, position, 1.0f, SystemClock.ElapsedRealtime());
            var playbackState = builder.Build();
            if (playbackState != null)
            {
                mediaSession?.SetPlaybackState(playbackState);
            }
        }
        logger.Debug("UpdatePlaybackState completed for state: {State}, position: {Position}, actions: {Actions}",
            state, position, actions);
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
        if (playbackState == null)
        {
            return;
        }

        // Only update position if playback is active
        var isActive = playbackState.State is PlaybackStateCompat.StatePlaying or
                      PlaybackStateCompat.StateBuffering or
                      PlaybackStateCompat.StatePaused;

        if (!isActive)
        {
            return;
        }

        var positionMs = (long)position.TotalMilliseconds;
        var durationMs = (long)duration.TotalMilliseconds;

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

        var builder = new PlaybackStateCompat.Builder();
        if (builder != null)
        {
            builder.SetActions(actions);
            builder.SetState(playbackState.State, positionMs, 1.0f, SystemClock.ElapsedRealtime());

            // Set duration in metadata if available
            if (durationMs > 0 && mediaSession?.Controller?.Metadata != null)
            {
                var metadataBuilder = new MediaMetadataCompat.Builder(mediaSession.Controller.Metadata);
                if (metadataBuilder != null)
                {
                    metadataBuilder.PutLong(MediaMetadataCompat.MetadataKeyDuration, durationMs);
                    var metadata = metadataBuilder.Build();
                    if (metadata != null)
                    {
                        mediaSession?.SetMetadata(metadata);
                    }
                }
            }

            var playbackStateCompat = builder.Build();
            if (playbackStateCompat != null)
            {
                mediaSession?.SetPlaybackState(playbackStateCompat);
            }
        }
    }

    /// <summary>
    /// Updates the metadata of the shared MediaSessionCompat.
    /// Preserves existing artwork and MediaId if present to prevent them from disappearing.
    /// </summary>
    public void UpdateMetadata(string title, string artist, string? album = null, int? scheduleId = null)
    {
        logger.Debug("UpdateMetadata called with title: {Title}, artist: {Artist}, album: {Album}, scheduleId: {ScheduleId}",
            title, artist, album ?? "null", scheduleId?.ToString() ?? "null");

        if (mediaSession == null)
        {
            logger.Warning("MediaSessionCompat is null, cannot update metadata. Call GetOrCreate() first.");
            return;
        }

        var builder = new MediaMetadataCompat.Builder()
            ?.PutString(MediaMetadataCompat.MetadataKeyTitle, title)
            ?.PutString(MediaMetadataCompat.MetadataKeyArtist, artist)
            ?.PutString(MediaMetadataCompat.MetadataKeyAlbum, album ?? "");

        if (builder == null)
        {
            logger.Warning("Failed to create MediaMetadataCompat.Builder");
            return;
        }

        // Preserve existing MediaId and artwork if present - don't overwrite them
        if (mediaSession?.Controller?.Metadata != null)
        {
            var existingMetadata = mediaSession.Controller.Metadata;

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

            // Preserve existing artwork
            global::Android.Graphics.Bitmap? existingArtwork = existingMetadata?.GetBitmap(MediaMetadataCompat.MetadataKeyArt);
            if (existingArtwork != null)
            {
                builder?.PutBitmap(MediaMetadataCompat.MetadataKeyArt, existingArtwork);
            }
        }
        else if (scheduleId.HasValue)
        {
            // Set MediaId if no existing metadata and scheduleId is provided
            builder?.PutString(MediaMetadataCompat.MetadataKeyMediaId, scheduleId.Value.ToString());
        }

        var metadata = builder?.Build();
        mediaSession?.SetMetadata(metadata);
    }

    /// <summary>
    /// Clears the metadata from MediaSessionCompat.
    /// This is recommended when playback stops to clean up the Android Auto interface.
    /// </summary>
    public void ClearMetadata()
    {
        mediaSession?.SetMetadata(null);
    }

    /// <summary>
    /// Sets a blank "loading" state for Android Auto:
    /// - clears metadata (no title/artist/artwork)
    /// - disables all transport controls (no tap/play while loading)
    /// - sets playback state to None (neutral) so hosts don't show "Tap to Open" style affordances
    /// </summary>
    public void SetBlankLoadingState()
    {
        if (mediaSession == null)
        {
            logger.Warning("MediaSessionCompat is null, cannot set loading state. Call GetOrCreate() first.");
            return;
        }

        try
        {
            AndroidAutoLoadingUiHelper.ApplyBlankLoadingState(mediaSession, global::Android.App.Application.Context);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error setting blank loading state");
        }
    }

    /// <summary>
    /// Sets a non-interactive buffering/loading state.
    /// This can be used during transitions (user clicked a schedule, skip next/prev) before metadata is ready.
    /// </summary>
    public void SetBufferingNoControlsState()
    {
        if (mediaSession == null)
        {
            logger.Warning("MediaSessionCompat is null, cannot set buffering state. Call GetOrCreate() first.");
            return;
        }

        try
        {
            // IMPORTANT:
            // When switching schedules, Android Auto will otherwise keep showing the previous schedule's
            // title/artwork until the new track's metadata arrives. Force the UI into a neutral state
            // (art-only, no text) during buffering.
            AndroidAutoLoadingUiHelper.ApplyBlankLoadingState(mediaSession, global::Android.App.Application.Context);

            // Disable all controls while buffering so AA doesn't show tappable UI.
            var builder = new PlaybackStateCompat.Builder()
                ?.SetActions(0)
                ?.SetState(PlaybackStateCompat.StateBuffering, 0, 0.0f, SystemClock.ElapsedRealtime());

            mediaSession?.SetPlaybackState(builder?.Build());
            SetActive(false);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error setting buffering no-controls state");
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

        var builder = new PlaybackStateCompat.Builder()
            ?.SetActions(PlaybackStateCompat.ActionPlay) // Only allow Play action when stopped
            ?.SetState(
                PlaybackStateCompat.StateStopped,
                0, // Playback position (0 when stopped)
                1.0f, // Playback speed (1.0f is standard)
                SystemClock.ElapsedRealtime() // Elapsed time since boot - required timestamp
            );

        mediaSession?.SetPlaybackState(builder?.Build());
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
            // When stopped/ended, set metadata for next schedule instead of clearing
            UpdatePlaybackStateForStop();
            SetNextScheduleMetadata();
            SetActive(false);
            // Note: Audio focus is released globally by AudioFocusEffect when playback stops
            logger.Information("MediaSessionCompat set to stopped/inactive - Android Auto shows next schedule metadata");
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

    /// <summary>
    /// Sets metadata for the next schedule track to be played.
    /// Called when playback stops or ends to show the next available schedule in Android Auto.
    /// Preserves existing artwork if present.
    /// </summary>
    private void SetNextScheduleMetadata()
    {
        try
        {
            var defaultScheduleService = serviceProvider.GetRequiredService<IDefaultScheduleService>();
            var metadataTask = defaultScheduleService.GetNextScheduleTrackMetaDataAsync();

            // Fire and forget - don't block the callback thread
            _ = Task.Run(async () =>
            {
                try
                {
                    var metadata = await metadataTask;

                    // Update metadata on main thread
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        // Check if playback is currently active - if so, don't overwrite current schedule metadata
                        var playbackState = mediaSession?.Controller?.PlaybackState;
                        var isPlaying = playbackState?.State is PlaybackStateCompat.StatePlaying or
                                       PlaybackStateCompat.StateBuffering or
                                       PlaybackStateCompat.StatePaused;

                        if (isPlaying)
                        {
                            return;
                        }

                        // Check if the current metadata's scheduleId matches the next schedule's ID
                        // If they match, we're already showing the correct metadata
                        var currentMediaId = mediaSession?.Controller?.Metadata?.GetString(MediaMetadataCompat.MetadataKeyMediaId);
                        if (!string.IsNullOrEmpty(currentMediaId) && currentMediaId == metadata.ScheduleId.ToString())
                        {
                            return;
                        }

                        // Preserve existing artwork if present
                        global::Android.Graphics.Bitmap? existingArtwork = null;
                        if (mediaSession?.Controller?.Metadata != null)
                        {
                            existingArtwork = mediaSession.Controller.Metadata?.GetBitmap(MediaMetadataCompat.MetadataKeyArt);
                        }

                        // Set mediaId to scheduleId for OnPlayFromMediaId
                        var metadataBuilder = new MediaMetadataCompat.Builder()
                            ?.PutString(MediaMetadataCompat.MetadataKeyTitle, metadata.Title)
                            ?.PutString(MediaMetadataCompat.MetadataKeyArtist, metadata.Artist)
                            ?.PutString(MediaMetadataCompat.MetadataKeyAlbum, metadata.Album ?? "")
                            ?.PutString(MediaMetadataCompat.MetadataKeyMediaId, metadata.ScheduleId.ToString());

                        // Preserve existing artwork if present
                        if (existingArtwork != null)
                        {
                            metadataBuilder?.PutBitmap(MediaMetadataCompat.MetadataKeyArt, existingArtwork);
                        }

                        mediaSession?.SetMetadata(metadataBuilder?.Build());
                    });
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error setting next schedule metadata");
                    // Fallback to clearing metadata if there's an error
                    ClearMetadata();
                }
            });
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting default schedule service for next schedule metadata");
            // Fallback to clearing metadata if there's an error
            ClearMetadata();
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

