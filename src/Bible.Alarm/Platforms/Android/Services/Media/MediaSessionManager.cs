#nullable enable
using Android.App;
using Android.Content;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Media;

/// <summary>
/// Singleton manager for the shared MediaSessionCompat instance.
/// Both Legacy and Modern Android Auto services must use the same MediaSessionCompat
/// to ensure seamless playback continuity and proper control handling.
/// </summary>
public sealed class MediaSessionManager
{
    public static readonly MediaSessionManager Instance = new();

    private MediaSessionCompat? _mediaSession;
    private readonly object _lock = new();

    private MediaSessionManager() { }

    /// <summary>
    /// Gets or creates the shared MediaSessionCompat instance.
    /// This is the single source of truth for media playback state across the entire app.
    /// </summary>
    public MediaSessionCompat GetOrCreate()
    {
        if (_mediaSession != null) return _mediaSession;

        lock (_lock)
        {
            if (_mediaSession != null) return _mediaSession;

            var context = global::Android.App.Application.Context;
            var logger = Log.ForContext<MediaSessionManager>();

            logger.Information("Creating shared MediaSessionCompat instance");

            _mediaSession = new MediaSessionCompat(context, "BibleAlarmSession", null, null);
            _mediaSession.SetFlags(
                MediaSessionCompat.FlagHandlesMediaButtons |
                MediaSessionCompat.FlagHandlesTransportControls);

            _mediaSession.SetCallback(new MediaSessionCallback());
            _mediaSession.Active = true;

            // Start with paused state — prevents auto-play on bind
            UpdatePlaybackState(PlaybackStateCompat.StatePaused);

            logger.Information("MediaSessionCompat created and activated");

            return _mediaSession;
        }
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
    /// Gets the SessionToken from the shared MediaSessionCompat.
    /// This token is used by both Legacy and Modern Android Auto services.
    /// </summary>
    public MediaSessionCompat.Token Token => _mediaSession!.SessionToken;
}
