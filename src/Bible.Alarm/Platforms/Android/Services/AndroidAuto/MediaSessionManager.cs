#nullable enable
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
    private MediaSessionCompat? _mediaSession;
    private readonly object _lock = new();
    private static readonly ILogger Logger = Log.ForContext<MediaSessionManager>();
    private readonly IServiceProvider _serviceProvider;

    public MediaSessionManager(IServiceProvider serviceProvider)
    {
        Logger.Debug("MediaSessionManager constructor called with serviceProvider: {ServiceProvider}", serviceProvider != null ? "provided" : "null");
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }
   
    /// <summary>
    /// Gets or creates the shared MediaSessionCompat instance.
    /// This is the single source of truth for media playback state across the entire app.
    /// Starts in stopped/inactive state. The MediaSessionEffect will update it when playback status changes.
    /// </summary>
    public MediaSessionCompat GetOrCreate(bool isConnect = false)
    {
        Logger.Debug("GetOrCreate called with isConnect: {IsConnect}", isConnect);
        // Fast path: if already created, return it
        if (_mediaSession == null)
        {
            // Double-checked locking pattern for thread safety
            lock (_lock)
            {
                // Check again inside lock (another thread might have created it)
                if (_mediaSession == null)
                {
                    // Create the MediaSessionCompat instance
                    var context = global::Android.App.Application.Context;
                    if (context == null)
                    {
                        Logger.Error("Android Application.Context is null - cannot create MediaSessionCompat");
                        throw new InvalidOperationException("Android Application.Context is null");
                    }

                    Logger.Information("Creating shared MediaSessionCompat instance");

                    _mediaSession = new MediaSessionCompat(context, "BibleAlarmSession", null, null);
                    if (_mediaSession == null)
                    {
                        Logger.Error("Failed to create MediaSessionCompat instance");
                        throw new InvalidOperationException("Failed to create MediaSessionCompat instance");
                    }

                    _mediaSession.SetFlags(
                        MediaSessionCompat.FlagHandlesMediaButtons |
                        MediaSessionCompat.FlagHandlesTransportControls);

                    // CRITICAL: SetCallback must be called on the main thread (requires Looper)
                    // Create MediaSessionCallback lazily to avoid startup dependency resolution issues
                    var playbackService = _serviceProvider.GetRequiredService<IPlaybackService>();
                    var logger = _serviceProvider.GetRequiredService<ILogger>();
                    var mediaSessionCallback = new MediaSessionCallback(playbackService, logger);
                    
                    // Ensure SetCallback runs on main thread to avoid Looper exception
                    if (MainThread.IsMainThread)
                    {
                        // The preferred path: if we are on the main thread, execute immediately.
                        _mediaSession.SetCallback(mediaSessionCallback);
                        Logger.Debug("MediaSessionCallback set synchronously on main thread");
                    }
                    else
                    {
                        // CRITICAL: Ensure SetCallback is run on the main thread without blocking the current Binder thread.
                        // The callback will be set asynchronously. The MediaSession will be operational shortly after.
                        // Android Auto is tolerant of this slight delay, and blocking the Binder thread causes deadlocks/ANRs.
                        Logger.Warning("MediaSession creation not on MainThread. Invoking SetCallback asynchronously to avoid blocking Binder thread.");
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            try
                            {
                                _mediaSession?.SetCallback(mediaSessionCallback);
                                Logger.Information("Successfully set MediaSessionCallback on main thread (async)");
                            }
                            catch (Exception ex)
                            {
                                Logger.Error(ex, "Error setting MediaSessionCallback asynchronously");
                            }
                        });
                        // NO BLOCKING CALL HERE (e.g., .Wait() or Task.Run().Wait())
                        // The Binder thread returns immediately, allowing Android Auto to complete binding without timeout.
                    }

                    // Start with stopped state — prevents auto-play on bind
                    // The effect will update this when it receives playback status changes
                    UpdatePlaybackState(PlaybackStateCompat.StateStopped);

                    // Verify SessionToken is available
                    if (_mediaSession.SessionToken == null)
                    {
                        Logger.Error("MediaSessionCompat.SessionToken is null after creation - this should not happen");
                        throw new InvalidOperationException("MediaSessionCompat.SessionToken is null after creation");
                    }

                    Logger.Information("MediaSessionCompat created successfully. Initial state: Stopped, Active: False, SessionToken available: {HasToken}",
                        _mediaSession.SessionToken != null);
                }
            }
        }

        Logger.Debug("GetOrCreate returning existing MediaSessionCompat instance");
        return _mediaSession;
    }


    /// <summary>
    /// Updates the playback state of the shared MediaSessionCompat.
    /// </summary>
    public void UpdatePlaybackState(int state, long position = 0)
    {
        Logger.Debug("UpdatePlaybackState called with state: {State}, position: {Position}", state, position);
        var builder = new PlaybackStateCompat.Builder()
            .SetActions(PlaybackStateCompat.ActionPlay |
                        PlaybackStateCompat.ActionPause |
                        PlaybackStateCompat.ActionSkipToNext |
                        PlaybackStateCompat.ActionSkipToPrevious |
                        PlaybackStateCompat.ActionPlayPause |
                        PlaybackStateCompat.ActionPlayFromMediaId)
            .SetState(state, position, 1.0f);

        _mediaSession?.SetPlaybackState(builder.Build());
        Logger.Debug("UpdatePlaybackState completed for state: {State}, position: {Position}", state, position);
    }

    /// <summary>
    /// Updates the metadata of the shared MediaSessionCompat.
    /// </summary>
    public void UpdateMetadata(string title, string artist, string? album = null)
    {
        Logger.Debug("UpdateMetadata called with title: {Title}, artist: {Artist}, album: {Album}", title, artist, album ?? "null");
        var metadata = new MediaMetadataCompat.Builder()
            .PutString(MediaMetadataCompat.MetadataKeyTitle, title)
            .PutString(MediaMetadataCompat.MetadataKeyArtist, artist)
            .PutString(MediaMetadataCompat.MetadataKeyAlbum, album ?? "")
            .Build();

        _mediaSession?.SetMetadata(metadata);
        Logger.Debug("UpdateMetadata completed for title: {Title}, artist: {Artist}", title, artist);
    }

    /// <summary>
    /// Clears the metadata from MediaSessionCompat.
    /// This is recommended when playback stops to clean up the Android Auto interface.
    /// </summary>
    public void ClearMetadata()
    {
        Logger.Debug("ClearMetadata called");
        _mediaSession?.SetMetadata(null);
        Logger.Debug("ClearMetadata completed");
    }

    /// <summary>
    /// Updates playback state to STOPPED with only Play action available.
    /// This removes playback controls from Android Auto screen when playback ends.
    /// </summary>
    public void UpdatePlaybackStateForStop()
    {
        Logger.Debug("UpdatePlaybackStateForStop called");
        var builder = new PlaybackStateCompat.Builder()
            .SetActions(PlaybackStateCompat.ActionPlay) // Only allow Play action when stopped
            .SetState(
                PlaybackStateCompat.StateStopped,
                0, // Playback position (0 when stopped)
                1.0f, // Playback speed (1.0f is standard)
                SystemClock.ElapsedRealtime() // Elapsed time since boot - required timestamp
            );

        _mediaSession?.SetPlaybackState(builder.Build());
        Logger.Debug("UpdatePlaybackStateForStop completed");
    }

    /// <summary>
    /// Sets the playback status, updating MediaSessionCompat active state and audio focus accordingly.
    /// </summary>
    public void SetPlaybackStatus(PlayStatus status)
    {
        Logger.Debug("SetPlaybackStatus called with status: {Status}", status);
        if (_mediaSession == null)
        {
            Logger.Warning("MediaSessionCompat is null, cannot set playback status. Call GetOrCreate() first.");
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

        if (status == PlayStatus.Stopped || status == PlayStatus.Ended)
        {
            // When stopped/ended, set metadata for next schedule instead of clearing
            UpdatePlaybackStateForStop();
            SetNextScheduleMetadata();
            SetActive(false);
            // Note: Audio focus is released globally by AudioFocusEffect when playback stops
            Logger.Information("MediaSessionCompat set to stopped/inactive - Android Auto shows next schedule metadata");
        }
        else
        {
            // Update playback state normally for other states
            UpdatePlaybackState(state);

            // Set MediaSessionCompat active when playing - this is critical for Android Auto audio routing
            // Note: Audio focus is managed globally by AudioFocusEffect, not here
            if (status == PlayStatus.Playing)
            {
                SetActive(true);
                Logger.Debug("MediaSessionCompat set to active (playing) - Android Auto can now route audio");
            }
            // Keep session active when paused (allows resume)
        }

        Logger.Debug("MediaSessionCompat playback status updated to: {Status} (State: {State})", status, state);
        Logger.Debug("SetPlaybackStatus completed for status: {Status}", status);
    }

    /// <summary>
    /// Sets metadata for the next schedule track to be played.
    /// Called when playback stops or ends to show the next available schedule in Android Auto.
    /// </summary>
    private void SetNextScheduleMetadata()
    {
        Logger.Debug("SetNextScheduleMetadata called");
        try
        {
            var defaultScheduleService = _serviceProvider.GetRequiredService<IDefaultScheduleService>();
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
                        UpdateMetadata(metadata.Title, metadata.Artist, metadata.Album);
                        
                        // Set mediaId to scheduleId for OnPlayFromMediaId
                        var metadataBuilder = new MediaMetadataCompat.Builder()
                            .PutString(MediaMetadataCompat.MetadataKeyTitle, metadata.Title)
                            .PutString(MediaMetadataCompat.MetadataKeyArtist, metadata.Artist)
                            .PutString(MediaMetadataCompat.MetadataKeyAlbum, metadata.Album ?? "")
                            .PutString(MediaMetadataCompat.MetadataKeyMediaId, metadata.ScheduleId.ToString());
                        
                        _mediaSession?.SetMetadata(metadataBuilder.Build());
                        
                        Logger.Information("Set next schedule metadata: ScheduleId={ScheduleId}, Title={Title}, Artist={Artist}", 
                            metadata.ScheduleId, metadata.Title, metadata.Artist);
                    });
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error setting next schedule metadata");
                    // Fallback to clearing metadata if there's an error
                    ClearMetadata();
                }
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error getting default schedule service for next schedule metadata");
            // Fallback to clearing metadata if there's an error
            ClearMetadata();
        }
    }

    internal void SetActive(bool active)
    {
        if (_mediaSession == null)
        {
            Logger.Warning("MediaSessionCompat is null, cannot set active state. Call GetOrCreate() first.");
            return;
        }
        _mediaSession.Active = active;
    }


    /// <summary>
    /// Gets the SessionToken from the shared MediaSessionCompat.
    /// This token is used by both Legacy and Modern Android Auto services.
    /// </summary>
    public MediaSessionCompat.Token Token
    {
        get
        {
            Logger.Debug("Token property accessed");
            var token = _mediaSession!.SessionToken;
            Logger.Debug("Token property returning token: {Token}", token != null ? "available" : "null");
            return token;
        }
    }
}

