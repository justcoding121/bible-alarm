#nullable enable
using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores;
using Fluxor;
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
   
    /// <summary>
    /// Gets or creates the shared MediaSessionCompat instance.
    /// This is the single source of truth for media playback state across the entire app.
    /// Starts in stopped/inactive state. The MediaSessionEffect will update it when playback status changes.
    /// </summary>
    public MediaSessionCompat GetOrCreate(bool isConnect = false)
    {
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

                    _mediaSession.SetCallback(new MediaSessionCallback());

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

        return _mediaSession;
    }


    /// <summary>
    /// Updates the playback state of the shared MediaSessionCompat.
    /// </summary>
    public void UpdatePlaybackState(int state, long position = 0)
    {
        var builder = new PlaybackStateCompat.Builder()
            .SetActions(PlaybackStateCompat.ActionPlay |
                        PlaybackStateCompat.ActionPause |
                        PlaybackStateCompat.ActionSkipToNext |
                        PlaybackStateCompat.ActionSkipToPrevious |
                        PlaybackStateCompat.ActionPlayPause |
                        PlaybackStateCompat.ActionPlayFromMediaId)
            .SetState(state, position, 1.0f);

        _mediaSession?.SetPlaybackState(builder.Build());
    }

    /// <summary>
    /// Updates the metadata of the shared MediaSessionCompat.
    /// </summary>
    public void UpdateMetadata(string title, string artist, string? album = null)
    {
        var metadata = new MediaMetadataCompat.Builder()
            .PutString(MediaMetadataCompat.MetadataKeyTitle, title)
            .PutString(MediaMetadataCompat.MetadataKeyArtist, artist)
            .PutString(MediaMetadataCompat.MetadataKeyAlbum, album ?? "")
            .Build();

        _mediaSession?.SetMetadata(metadata);
    }

    /// <summary>
    /// Clears the metadata from MediaSessionCompat.
    /// This is recommended when playback stops to clean up the Android Auto interface.
    /// </summary>
    public void ClearMetadata()
    {
        _mediaSession?.SetMetadata(null);
    }

    /// <summary>
    /// Updates playback state to STOPPED with only Play action available.
    /// This removes playback controls from Android Auto screen when playback ends.
    /// </summary>
    public void UpdatePlaybackStateForStop()
    {
        var builder = new PlaybackStateCompat.Builder()
            .SetActions(PlaybackStateCompat.ActionPlay) // Only allow Play action when stopped
            .SetState(
                PlaybackStateCompat.StateStopped,
                0, // Playback position (0 when stopped)
                1.0f, // Playback speed (1.0f is standard)
                SystemClock.ElapsedRealtime() // Elapsed time since boot - required timestamp
            );

        _mediaSession?.SetPlaybackState(builder.Build());
    }

    /// <summary>
    /// Sets the playback status, updating MediaSessionCompat active state and audio focus accordingly.
    /// </summary>
    public void SetPlaybackStatus(PlayStatus status)
    {
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
            // When stopped/ended, remove playback controls from Android Auto screen
            UpdatePlaybackStateForStop();
            ClearMetadata();
            SetActive(false);
            // Note: Audio focus is released globally by AudioFocusEffect when playback stops
            Logger.Information("MediaSessionCompat set to stopped/inactive - Android Auto playback controls removed");
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
    public MediaSessionCompat.Token Token => _mediaSession!.SessionToken;
}

