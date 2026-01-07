#nullable enable
using Bible.Alarm.Services.Media.Models;
using Foundation;
using MediaPlayer;
using Serilog;
using UIKit;

namespace Bible.Alarm.Platforms.iOS.Services.Media;

/// <summary>
/// Manages MPNowPlayingInfoCenter for iOS Now Playing display.
/// Updates the Lock Screen, Control Center, CarPlay, and other system media interfaces
/// with current track metadata and playback progress.
/// </summary>
public sealed class iOSNowPlayingInfoManager
{
    private static readonly ILogger logger = Log.ForContext<iOSNowPlayingInfoManager>();

    // Cache current metadata to avoid redundant updates
    private string? currentTitle;
    private string? currentArtist;
    private string? currentAlbum;
    private double currentDuration;
    private PlayStatus currentStatus = PlayStatus.Stopped;

    /// <summary>
    /// Updates the Now Playing metadata (title, artist, album, artwork).
    /// Call this when the track changes.
    /// </summary>
    public void UpdateMetadata(string? title, string? artist, string? album, TimeSpan duration, string? artworkUrl)
    {
        try
        {
            logger.Debug("[iOS NowPlaying] Updating metadata: Title={Title}, Artist={Artist}, Album={Album}, Duration={Duration}",
                title, artist, album, duration);

            currentTitle = title;
            currentArtist = artist;
            currentAlbum = album;
            currentDuration = duration.TotalSeconds;

            var nowPlayingInfo = CreateNowPlayingInfo(title, artist, album, duration, artworkUrl);
            MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = nowPlayingInfo;

            logger.Information("[iOS NowPlaying] Metadata updated successfully");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[iOS NowPlaying] Failed to update metadata");
        }
    }

    /// <summary>
    /// Updates the playback position and rate.
    /// Call this periodically during playback (every few seconds) and on pause/resume.
    /// </summary>
    public void UpdatePlaybackPosition(TimeSpan currentPosition, TimeSpan duration, PlayStatus status)
    {
        try
        {
            currentStatus = status;
            currentDuration = duration.TotalSeconds;

            var nowPlayingInfo = MPNowPlayingInfoCenter.DefaultCenter.NowPlaying;
            if (nowPlayingInfo == null)
            {
                // Create new info if none exists
                nowPlayingInfo = new MPNowPlayingInfo();
            }

            // Update position and rate
            nowPlayingInfo.ElapsedPlaybackTime = currentPosition.TotalSeconds;
            nowPlayingInfo.PlaybackDuration = duration.TotalSeconds;
            nowPlayingInfo.PlaybackRate = status == PlayStatus.Playing ? 1.0 : 0.0;

            MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = nowPlayingInfo;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[iOS NowPlaying] Failed to update playback position");
        }
    }

    /// <summary>
    /// Updates the playback rate (playing/paused state).
    /// Call this when play/pause state changes.
    /// </summary>
    public void UpdatePlaybackStatus(PlayStatus status)
    {
        try
        {
            currentStatus = status;

            var nowPlayingInfo = MPNowPlayingInfoCenter.DefaultCenter.NowPlaying;
            if (nowPlayingInfo == null)
            {
                return;
            }

            // Update playback rate: 1.0 = playing, 0.0 = paused/stopped
            nowPlayingInfo.PlaybackRate = status == PlayStatus.Playing ? 1.0 : 0.0;

            MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = nowPlayingInfo;

            logger.Debug("[iOS NowPlaying] Playback status updated: {Status}, Rate={Rate}",
                status, nowPlayingInfo.PlaybackRate);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[iOS NowPlaying] Failed to update playback status");
        }
    }

    /// <summary>
    /// Updates the duration (when track duration becomes known after buffering).
    /// </summary>
    public void UpdateDuration(TimeSpan duration)
    {
        try
        {
            currentDuration = duration.TotalSeconds;

            var nowPlayingInfo = MPNowPlayingInfoCenter.DefaultCenter.NowPlaying;
            if (nowPlayingInfo == null)
            {
                return;
            }

            nowPlayingInfo.PlaybackDuration = duration.TotalSeconds;
            MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = nowPlayingInfo;

            logger.Debug("[iOS NowPlaying] Duration updated: {Duration}", duration);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[iOS NowPlaying] Failed to update duration");
        }
    }

    /// <summary>
    /// Clears the Now Playing info.
    /// Call this when playback stops completely.
    /// </summary>
    public void ClearNowPlayingInfo()
    {
        try
        {
            MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = null!;
            currentTitle = null;
            currentArtist = null;
            currentAlbum = null;
            currentDuration = 0;
            currentStatus = PlayStatus.Stopped;

            logger.Debug("[iOS NowPlaying] Now Playing info cleared");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[iOS NowPlaying] Failed to clear Now Playing info");
        }
    }

    /// <summary>
    /// Sets default metadata for when no playback is active (e.g., for CarPlay idle screen).
    /// </summary>
    public void SetDefaultMetadata(string? title, string? artist, string? album, string? artworkUrl)
    {
        try
        {
            logger.Debug("[iOS NowPlaying] Setting default metadata: Title={Title}, Artist={Artist}",
                title, artist);

            var nowPlayingInfo = CreateNowPlayingInfo(title, artist, album, TimeSpan.Zero, artworkUrl);
            nowPlayingInfo.PlaybackRate = 0.0; // Not playing
            MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = nowPlayingInfo;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[iOS NowPlaying] Failed to set default metadata");
        }
    }

    private MPNowPlayingInfo CreateNowPlayingInfo(string? title, string? artist, string? album, TimeSpan duration, string? artworkUrl)
    {
        var info = new MPNowPlayingInfo
        {
            Title = title ?? "Bible Alarm",
            Artist = artist ?? string.Empty,
            AlbumTitle = album ?? string.Empty,
            PlaybackDuration = duration.TotalSeconds,
            ElapsedPlaybackTime = 0,
            PlaybackRate = currentStatus == PlayStatus.Playing ? 1.0 : 0.0,
            MediaType = MPNowPlayingInfoMediaType.Audio
        };

        // Load artwork if URL is provided
        if (!string.IsNullOrEmpty(artworkUrl))
        {
            Task.Run(async () =>
            {
                try
                {
                    var artwork = await LoadArtworkAsync(artworkUrl);
                    if (artwork != null)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            var currentInfo = MPNowPlayingInfoCenter.DefaultCenter.NowPlaying;
                            if (currentInfo != null)
                            {
                                currentInfo.Artwork = artwork;
                                MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = currentInfo;
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "[iOS NowPlaying] Failed to load artwork from {Url}", artworkUrl);
                }
            });
        }

        return info;
    }

    private async Task<MPMediaItemArtwork?> LoadArtworkAsync(string artworkUrl)
    {
        try
        {
            using var httpClient = new HttpClient();
            var imageData = await httpClient.GetByteArrayAsync(artworkUrl);
            var nsData = NSData.FromArray(imageData);
            var image = UIImage.LoadFromData(nsData);

            if (image != null)
            {
                return new MPMediaItemArtwork(image.Size, _ => image);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "[iOS NowPlaying] Failed to download artwork from {Url}", artworkUrl);
        }

        return null;
    }
}
