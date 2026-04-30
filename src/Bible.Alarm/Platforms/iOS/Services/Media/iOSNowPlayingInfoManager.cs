#nullable enable
using Bible.Alarm.Platforms.iOS.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Services.Media.Models;
using CoreGraphics;
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
    private double currentPosition;
    private PlayStatus currentStatus = PlayStatus.Stopped;

    // Prevent the source UIImage from being garbage-collected while the
    // MPMediaItemArtwork handler still references it across the managed/native boundary.
    private UIImage? retainedArtworkImage;

    /// <summary>
    /// Updates the Now Playing metadata (title, artist, album, artwork).
    /// Call this when the track changes.
    /// Previous artwork is preserved during transitions while async artwork extraction completes.
    /// If no artwork is ever available, the car/lock screen shows no art (no app icon fallback).
    /// </summary>
    public void UpdateMetadata(string? title, string? artist, string? album, TimeSpan duration, string? artworkUrl)
    {
        try
        {
            currentTitle = title;
            currentArtist = artist;
            currentAlbum = album;
            currentDuration = duration.TotalSeconds;

            // Preserve previous artwork as placeholder during transitions
            var previousArtwork = currentArtwork;
            var oldArtworkUrl = currentArtworkUrl;

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
                        // Failed to load synchronously - keep previous as placeholder, try async
                        LoadArtworkAsyncAndUpdate(artworkUrl);
                    }
                }
            }

            var nowPlayingInfo = CreateNowPlayingInfoWithArtwork(title, artist, album, duration, artworkToUse);
            MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = nowPlayingInfo;
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.IosNowPlayingDiagnosticsLog.FailedToUpdateMetadata);
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
                        try
                        {
                            var currentInfo = MPNowPlayingInfoCenter.DefaultCenter.NowPlaying;
                            if (currentInfo != null)
                            {
                                currentInfo.Artwork = asyncArtwork;
                                currentArtwork = asyncArtwork;
                                MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = currentInfo;
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Debug(ex, AppConstants.Logging.IosNowPlayingDiagnosticsLog.FailedToApplyAsyncArtworkTransitional);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, AppConstants.Logging.IosNowPlayingDiagnosticsLog.FailedToLoadArtworkAsyncFromUrl, artworkUrl);
            }
        });
    }

    /// <summary>
    /// Maximum lengths for Now Playing text to avoid overlap on CarPlay (title and two-line subtitle in fixed areas).
    /// Clipping keeps title/artist to single-line display; verify on a real CarPlay connection when possible.
    /// </summary>
    private const int MaxTitleLength = 35;
    private const int MaxArtistLength = 40;
    private const int MaxAlbumLength = 40;

    /// <summary>
    /// Clips text to a single line (strips newlines) and truncates to maxLength to limit multiline on Now Playing.
    /// </summary>
    private static string ClipToSingleLine(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var singleLine = value
            .Replace("\r\n", " ")
            .Replace("\n", " ")
            .Replace("\r", " ")
            .Trim();

        if (singleLine.Length <= maxLength)
            return singleLine;
        return singleLine.Substring(0, maxLength - 1).TrimEnd() + "…";
    }

    private MPNowPlayingInfo CreateNowPlayingInfoWithArtwork(string? title, string? artist, string? album, TimeSpan duration, MPMediaItemArtwork? artwork)
    {
        var displayTitle = ClipToSingleLine(title, MaxTitleLength);
        var displayArtist = ClipToSingleLine(artist, MaxArtistLength);
        var displayAlbum = ClipToSingleLine(album, MaxAlbumLength);

        var info = new MPNowPlayingInfo
        {
            Title = string.IsNullOrEmpty(displayTitle) ? AppConstants.AppSettings.ApplicationDisplayName : displayTitle,
            Artist = displayArtist,
            AlbumTitle = displayAlbum,
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

    private static double GetPlaybackDurationSeconds(MPNowPlayingInfo info)
    {
        var d = info.PlaybackDuration;
        return d.HasValue ? d.Value : 0d;
    }

    private static double GetElapsedPlaybackSeconds(MPNowPlayingInfo info)
    {
        var e = info.ElapsedPlaybackTime;
        return e ?? 0;
    }

    /// <summary>
    /// Creates a fresh MPNowPlayingInfo from cached metadata.
    /// iOS can drop artwork when we modify a retrieved NowPlaying object and set it back;
    /// building a fresh instance from our cache ensures artwork is always preserved on pause.
    /// </summary>
    private MPNowPlayingInfo? CreateFreshNowPlayingInfoFromCache(double durationSeconds, double elapsedSeconds, double playbackRate)
    {
        if (string.IsNullOrEmpty(currentTitle))
        {
            return null;
        }

        var info = CreateNowPlayingInfoWithArtwork(currentTitle, currentArtist, currentAlbum, TimeSpan.FromSeconds(durationSeconds), currentArtwork);
        info.ElapsedPlaybackTime = elapsedSeconds;
        info.PlaybackRate = playbackRate;
        return info;
    }

    /// <summary>
    /// Updates the playback position and rate.
    /// Call this periodically during playback (every few seconds) and on pause/resume.
    /// Creates a fresh MPNowPlayingInfo from cache to avoid iOS dropping artwork when paused.
    /// </summary>
    public void UpdatePlaybackPosition(TimeSpan currentPosition, TimeSpan duration, PlayStatus status)
    {
        try
        {
            if (status is PlayStatus.Stopped or PlayStatus.Ended)
            {
                return;
            }

            currentStatus = status;
            currentDuration = duration.TotalSeconds;
            this.currentPosition = currentPosition.TotalSeconds;

            // Ensure artwork is loaded if we have URL but not cached yet
            if (currentArtwork == null && !string.IsNullOrEmpty(currentArtworkUrl))
            {
                var artwork = LoadArtworkSync(currentArtworkUrl);
                if (artwork != null)
                {
                    currentArtwork = artwork;
                }
            }

            var freshInfo = CreateFreshNowPlayingInfoFromCache(
                currentDuration,
                this.currentPosition,
                status == PlayStatus.Playing ? 1.0 : 0.0);
            if (freshInfo != null)
            {
                MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = freshInfo;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.IosNowPlayingDiagnosticsLog.FailedToUpdatePlaybackPosition);
        }
    }

    /// <summary>
    /// Updates the playback rate (playing/paused state).
    /// Call this when play/pause state changes.
    /// Creates a fresh MPNowPlayingInfo from cache instead of modifying the retrieved object;
    /// iOS drops artwork when the retrieved NowPlaying is modified and set back.
    /// </summary>
    public void UpdatePlaybackStatus(PlayStatus status)
    {
        try
        {
            currentStatus = status;

            var existingInfo = MPNowPlayingInfoCenter.DefaultCenter.NowPlaying;
            if (existingInfo == null)
            {
                return;
            }

            var durationSeconds = currentDuration > 0 ? currentDuration : GetPlaybackDurationSeconds(existingInfo);
            var elapsedSeconds = GetElapsedPlaybackSeconds(existingInfo);
            var rate = status == PlayStatus.Playing ? 1.0 : 0.0;

            var freshInfo = CreateFreshNowPlayingInfoFromCache(durationSeconds, elapsedSeconds, rate);
            if (freshInfo != null)
            {
                MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = freshInfo;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.IosNowPlayingDiagnosticsLog.FailedToUpdatePlaybackStatus);
        }
    }

    /// <summary>
    /// Updates the duration (when track duration becomes known after buffering).
    /// Creates a fresh MPNowPlayingInfo from cache to avoid iOS dropping artwork.
    /// </summary>
    public void UpdateDuration(TimeSpan duration)
    {
        try
        {
            currentDuration = duration.TotalSeconds;

            var existingInfo = MPNowPlayingInfoCenter.DefaultCenter.NowPlaying;
            if (existingInfo == null)
            {
                return;
            }

            var elapsedSeconds = GetElapsedPlaybackSeconds(existingInfo);
            var rate = currentStatus == PlayStatus.Playing ? 1.0 : 0.0;

            var freshInfo = CreateFreshNowPlayingInfoFromCache(currentDuration, elapsedSeconds, rate);
            if (freshInfo != null)
            {
                MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = freshInfo;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.IosNowPlayingDiagnosticsLog.FailedToUpdateDuration);
        }
    }

    public TimeSpan GetCurrentDuration()
    {
        return TimeSpan.FromSeconds(currentDuration);
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
            retainedArtworkImage = null;
            currentDuration = 0;
            currentPosition = 0;
            currentStatus = PlayStatus.Stopped;
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.IosNowPlayingDiagnosticsLog.FailedToClearNowPlayingInfo);
        }
    }

    /// <summary>
    /// Sets default metadata for when no playback is active (e.g., for lock screen/CarPlay idle screen).
    /// This allows users to start playback directly from lock screen controls.
    /// When no artwork URL is available or loading fails, CarPlay may show no artwork (no app icon fallback).
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
            logger.Error(ex, AppConstants.Logging.IosNowPlayingDiagnosticsLog.FailedToSetDefaultMetadata);
        }
    }

    private MPMediaItemArtwork? LoadArtworkSync(string artworkUrl)
    {
        try
        {
            // Handle file:// URLs
            string? filePath = null;
            if (artworkUrl.StartsWith(MediaUriSchemeConstants.FilePrefix, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var uri = new Uri(artworkUrl);
                    filePath = uri.LocalPath;
                }
                catch
                {
                    filePath = artworkUrl.Replace(MediaUriSchemeConstants.FilePrefix, "").Replace(MediaUriSchemeConstants.FileUriTripleSlashPrefix, "/");
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

            // Keep a strong managed reference so the GC does not collect the UIImage
            // while the native MPMediaItemArtwork handler closure still needs it.
            retainedArtworkImage = image;

            var boundsSize = new CGSize(Math.Max(image.Size.Width, 600), Math.Max(image.Size.Height, 600));
            return new MPMediaItemArtwork(boundsSize, requestedSize => ScaleImageToRequestedSize(image, requestedSize));
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.IosNowPlayingDiagnosticsLog.FailedToLoadArtworkSynchronouslyFromUrl, artworkUrl);
        }

        return null;
    }

    /// <summary>
    /// Scales a UIImage to fill the exact requested size (aspect-fill, centered).
    /// CarPlay and lock screen expect the returned image to match the requested
    /// dimensions; returning a smaller image can cause artwork to not display.
    /// </summary>
    private static UIImage ScaleImageToRequestedSize(UIImage image, CGSize requestedSize)
    {
        if (requestedSize.Width <= 0 || requestedSize.Height <= 0)
        {
            return image;
        }

        try
        {
            // Aspect-fill: scale so the image covers the entire requested rect,
            // then center-crop to the exact requested size.
            var scale = Math.Max(
                requestedSize.Width / image.Size.Width,
                requestedSize.Height / image.Size.Height);

            var scaledWidth = (nfloat)(image.Size.Width * scale);
            var scaledHeight = (nfloat)(image.Size.Height * scale);

            var drawX = (requestedSize.Width - scaledWidth) / 2;
            var drawY = (requestedSize.Height - scaledHeight) / 2;

            var renderer = new UIGraphicsImageRenderer(requestedSize);
            var scaledImage = renderer.CreateImage(_ =>
                image.Draw(new CGRect(drawX, drawY, scaledWidth, scaledHeight)));

            return scaledImage ?? image;
        }
        catch
        {
            return image;
        }
    }

    private async Task<MPMediaItemArtwork?> LoadArtworkAsync(string artworkUrl)
    {
        try
        {
            // Skip if it's a file path (should have been handled by sync method)
            if (artworkUrl.StartsWith(MediaUriSchemeConstants.FilePrefix, StringComparison.OrdinalIgnoreCase) ||
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
                retainedArtworkImage = image;
                var boundsSize = new CGSize(Math.Max(image.Size.Width, 600), Math.Max(image.Size.Height, 600));
                return new MPMediaItemArtwork(boundsSize, requestedSize => ScaleImageToRequestedSize(image, requestedSize));
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.IosNowPlayingDiagnosticsLog.FailedToDownloadArtworkFromUrl, artworkUrl);
        }

        return null;
    }
}
