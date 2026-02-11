#nullable enable
using Bible.Alarm.Platforms.iOS.Services.Media.Interfaces;
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
public sealed class iOSNowPlayingInfoManager : IiOSNowPlayingInfoManager
{
    private static readonly ILogger logger = Log.ForContext<iOSNowPlayingInfoManager>();

    // Cache current metadata to avoid redundant updates
    private string? currentTitle;
    private string? currentArtist;
    private string? currentAlbum;
    private string? currentArtworkUrl;
    private MPMediaItemArtwork? currentArtwork;
    private double currentDuration;
    private PlayStatus currentStatus = PlayStatus.Stopped;

    /// <summary>
    /// Updates the Now Playing metadata (title, artist, album, artwork).
    /// Call this when the track changes.
    /// Following iOS standard practice, artwork is never cleared - if loading new artwork fails,
    /// we keep the previous artwork to ensure there's always visual content on lock screen.
    /// </summary>
    public void UpdateMetadata(string? title, string? artist, string? album, TimeSpan duration, string? artworkUrl)
    {
        try
        {
            currentTitle = title;
            currentArtist = artist;
            currentAlbum = album;
            currentDuration = duration.TotalSeconds;

            // Preserve previous artwork reference - NEVER clear artwork
            var previousArtwork = currentArtwork;
            var oldArtworkUrl = currentArtworkUrl;

            // Always start with previous artwork as fallback (never show empty lock screen)
            MPMediaItemArtwork? artworkToUse = previousArtwork;

            // Try to load new artwork if URL is provided
            if (!string.IsNullOrEmpty(artworkUrl))
            {
                if (artworkUrl == oldArtworkUrl && currentArtwork != null)
                {
                    // Same URL, reuse cached artwork
                    artworkToUse = currentArtwork;
                }
                else if (artworkUrl != oldArtworkUrl)
                {
                    // URL changed, try to load new artwork
                    currentArtworkUrl = artworkUrl;
                    var newArtwork = LoadArtworkSync(artworkUrl);
                    if (newArtwork != null)
                    {
                        artworkToUse = newArtwork;
                        currentArtwork = newArtwork;
                    }
                    else
                    {
                        // Failed to load new artwork - keep previous artwork (never show empty)
                        LoadArtworkAsyncAndUpdate(artworkUrl);
                    }
                }
            }

            var nowPlayingInfo = CreateNowPlayingInfoWithArtwork(title, artist, album, duration, artworkToUse);
            MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = nowPlayingInfo;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[iOS NowPlaying] Failed to update metadata");
        }
    }

    private void LoadArtworkAsyncAndUpdate(string artworkUrl)
    {
        Task.Run(async () =>
        {
            try
            {
                var asyncArtwork = await LoadArtworkAsync(artworkUrl);
                if (asyncArtwork != null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        var currentInfo = MPNowPlayingInfoCenter.DefaultCenter.NowPlaying;
                        if (currentInfo != null)
                        {
                            currentInfo.Artwork = asyncArtwork;
                            currentArtwork = asyncArtwork;
                            MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = currentInfo;
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "[iOS NowPlaying] Failed to load artwork async from {Url}", artworkUrl);
            }
        });
    }

    private MPNowPlayingInfo CreateNowPlayingInfoWithArtwork(string? title, string? artist, string? album, TimeSpan duration, MPMediaItemArtwork? artwork)
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

        if (artwork != null)
        {
            info.Artwork = artwork;
        }

        return info;
    }

    /// <summary>
    /// Updates the playback position and rate.
    /// Call this periodically during playback (every few seconds) and on pause/resume.
    /// </summary>
    public void UpdatePlaybackPosition(TimeSpan currentPosition, TimeSpan duration, PlayStatus status)
    {
        try
        {
            // Skip if stopped to prevent recreating Now Playing after it's been cleared
            if (status == PlayStatus.Stopped)
            {
                return;
            }

            currentStatus = status;
            currentDuration = duration.TotalSeconds;

            var nowPlayingInfo = MPNowPlayingInfoCenter.DefaultCenter.NowPlaying;
            if (nowPlayingInfo == null)
            {
                // Only create new info if we have valid metadata (means playback is active)
                // If currentTitle is null, we've likely cleared the info and shouldn't recreate it
                if (string.IsNullOrEmpty(currentTitle))
                {
                    return;
                }

                // Create new info using cached metadata
                nowPlayingInfo = new MPNowPlayingInfo
                {
                    Title = currentTitle,
                    Artist = currentArtist ?? string.Empty,
                    AlbumTitle = currentAlbum ?? string.Empty,
                    MediaType = MPNowPlayingInfoMediaType.Audio
                };

                // Use cached artwork or load synchronously
                if (currentArtwork != null)
                {
                    nowPlayingInfo.Artwork = currentArtwork;
                }
                else if (!string.IsNullOrEmpty(currentArtworkUrl))
                {
                    var artwork = LoadArtworkSync(currentArtworkUrl);
                    if (artwork != null)
                    {
                        nowPlayingInfo.Artwork = artwork;
                        currentArtwork = artwork;
                    }
                }
            }

            // Update position and rate
            nowPlayingInfo.ElapsedPlaybackTime = currentPosition.TotalSeconds;
            nowPlayingInfo.PlaybackDuration = duration.TotalSeconds;
            nowPlayingInfo.PlaybackRate = status == PlayStatus.Playing ? 1.0 : 0.0;

            // Re-apply cached artwork - getting NowPlaying returns a copy that may lose the artwork
            if (currentArtwork != null)
            {
                nowPlayingInfo.Artwork = currentArtwork;
            }

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

            nowPlayingInfo.PlaybackRate = status == PlayStatus.Playing ? 1.0 : 0.0;

            // Re-apply cached artwork - getting NowPlaying returns a copy that may lose the artwork
            if (currentArtwork != null)
            {
                nowPlayingInfo.Artwork = currentArtwork;
            }

            MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = nowPlayingInfo;
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

            // Re-apply cached artwork - getting NowPlaying returns a copy that may lose the artwork
            if (currentArtwork != null)
            {
                nowPlayingInfo.Artwork = currentArtwork;
            }

            MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = nowPlayingInfo;
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
            currentArtworkUrl = null;
            currentArtwork = null;
            currentDuration = 0;
            currentStatus = PlayStatus.Stopped;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[iOS NowPlaying] Failed to clear Now Playing info");
        }
    }

    /// <summary>
    /// Sets default metadata for when no playback is active (e.g., for lock screen/CarPlay idle screen).
    /// This allows users to start playback directly from lock screen controls.
    /// Artwork is always preserved - we never show lock screen without artwork.
    /// </summary>
    public void SetDefaultMetadata(string? title, string? artist, string? album, string? artworkUrl)
    {
        try
        {
            currentTitle = title;
            currentArtist = artist;
            currentAlbum = album;
            currentStatus = PlayStatus.Stopped;
            currentDuration = 0;

            // Preserve previous artwork
            var previousArtwork = currentArtwork;

            MPMediaItemArtwork? artworkToUse = null;
            if (!string.IsNullOrEmpty(artworkUrl))
            {
                if (artworkUrl == currentArtworkUrl && currentArtwork != null)
                {
                    // Same URL, reuse cached artwork
                    artworkToUse = currentArtwork;
                }
                else
                {
                    // Try to load new artwork
                    currentArtworkUrl = artworkUrl;
                    var newArtwork = LoadArtworkSync(artworkUrl);
                    if (newArtwork != null)
                    {
                        artworkToUse = newArtwork;
                        currentArtwork = newArtwork;
                    }
                    else
                    {
                        // Failed to load, keep previous
                        artworkToUse = previousArtwork;
                        LoadArtworkAsyncAndUpdate(artworkUrl);
                    }
                }
            }
            else if (currentArtwork != null)
            {
                // No URL provided, keep current artwork
                artworkToUse = currentArtwork;
            }

            var nowPlayingInfo = CreateNowPlayingInfoWithArtwork(title, artist, album, TimeSpan.Zero, artworkToUse);
            nowPlayingInfo.PlaybackRate = 0.0;
            MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = nowPlayingInfo;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[iOS NowPlaying] Failed to set default metadata");
        }
    }

    private MPMediaItemArtwork? LoadArtworkSync(string artworkUrl)
    {
        try
        {
            // Handle file:// URLs
            string? filePath = null;
            if (artworkUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var uri = new Uri(artworkUrl);
                    filePath = uri.LocalPath;
                }
                catch
                {
                    filePath = artworkUrl.Replace("file://", "").Replace("file:///", "/");
                }
            }
            else if (System.IO.Path.IsPathRooted(artworkUrl))
            {
                filePath = artworkUrl;
            }
            else
            {
                // Try AppDataDirectory first, then CacheDirectory for backwards compatibility
                var appDataDir = FileSystem.AppDataDirectory;
                filePath = System.IO.Path.Combine(appDataDir, artworkUrl);

                if (!File.Exists(filePath))
                {
                    var cacheDir = FileSystem.CacheDirectory;
                    filePath = System.IO.Path.Combine(cacheDir, artworkUrl);
                }
            }

            if (string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            if (!File.Exists(filePath))
            {
                return null;
            }

            var nsData = NSData.FromFile(filePath);
            if (nsData == null || nsData.Length == 0)
            {
                return null;
            }

            var image = UIImage.LoadFromData(nsData);
            if (image == null)
            {
                return null;
            }

            // Use actual image size - iOS handles scaling automatically
            return new MPMediaItemArtwork(image.Size, _ => image);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "[iOS NowPlaying] Failed to load artwork synchronously from {Url}", artworkUrl);
        }

        return null;
    }

    private async Task<MPMediaItemArtwork?> LoadArtworkAsync(string artworkUrl)
    {
        try
        {
            // Skip if it's a file path (should have been handled by sync method)
            if (artworkUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
                System.IO.Path.IsPathRooted(artworkUrl))
            {
                return null;
            }

            // Handle HTTP/HTTPS URLs
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
