#nullable enable
using Bible;
using Serilog;

namespace Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;

/// <summary>
/// Handles artwork loading and management for the alarm modal.
/// </summary>
public sealed class ArtworkManager(ILogger logger)
{
    private ImageSource? artworkSource;
    private bool isArtworkLoading;
    private byte[]? artworkBytes;
    private string? lastArtworkUrl;

    /// <summary>
    /// Gets the current artwork source.
    /// </summary>
    public ImageSource? ArtworkSource => artworkSource;

    /// <summary>
    /// Gets whether artwork is currently loading.
    /// </summary>
    public bool IsArtworkLoading => isArtworkLoading;

    /// <summary>
    /// Gets whether artwork is available.
    /// </summary>
    public bool HasArtwork => ArtworkSource != null && !IsArtworkLoading;

    /// <summary>
    /// Updates the artwork from a URL.
    /// </summary>
    public void UpdateArtwork(string? artworkUrl, Action<ImageSource?> setArtworkSource, Action<bool> setIsArtworkLoading)
    {
        if (lastArtworkUrl == artworkUrl)
        {
            return;
        }

        var previousUrl = lastArtworkUrl;
        lastArtworkUrl = artworkUrl;

        if (string.IsNullOrEmpty(artworkUrl))
        {
            if (artworkSource != null)
            {
                ClearArtwork(setArtworkSource, setIsArtworkLoading);
            }
            return;
        }

        if (artworkSource != null && !string.IsNullOrEmpty(previousUrl) && previousUrl != artworkUrl)
        {
            ClearArtwork(setArtworkSource, setIsArtworkLoading);
        }

        setIsArtworkLoading(true);

        try
        {
            if (TryLoadFromUri(artworkUrl, setArtworkSource, setIsArtworkLoading))
            {
                return;
            }

            var filePath = ResolveFilePath(artworkUrl);
            if (!string.IsNullOrEmpty(filePath))
            {
                LoadFromFile(filePath, setArtworkSource, setIsArtworkLoading);
            }
            else
            {
                ClearArtwork(setArtworkSource, setIsArtworkLoading);
            }
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Error updating artwork from URL: {ArtworkUrl}", artworkUrl);
            ClearArtwork(setArtworkSource, setIsArtworkLoading);
        }
    }

    /// <summary>
    /// Clears the current artwork.
    /// </summary>
    private void ClearArtwork(Action<ImageSource?> setArtworkSource, Action<bool> setIsArtworkLoading)
    {
        setArtworkSource(null);
        artworkBytes = null;
        setIsArtworkLoading(false);
        lastArtworkUrl = null;
    }

    /// <summary>
    /// Attempts to load artwork from a URI.
    /// </summary>
    private bool TryLoadFromUri(string artworkUrl, Action<ImageSource?> setArtworkSource, Action<bool> setIsArtworkLoading)
    {
        if (!Uri.TryCreate(artworkUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            return false;
        }

        setArtworkSource(ImageSource.FromUri(uri));
        setIsArtworkLoading(false);
        return true;
    }

    /// <summary>
    /// Resolves a file path from an artwork URL.
    /// </summary>
    private string? ResolveFilePath(string artworkUrl)
    {
        // Handle file:// URIs
        if (artworkUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return new Uri(artworkUrl).LocalPath;
            }
            catch (Exception ex)
            {
                logger.Debug(ex, "Failed to convert file:// URI to local path: {ArtworkUrl}", artworkUrl);
                return null;
            }
        }

        // Handle direct file paths
        if (Path.IsPathRooted(artworkUrl))
        {
            return artworkUrl;
        }

        return null;
    }

    /// <summary>
    /// Loads artwork from a file path.
    /// </summary>
    private void LoadFromFile(string filePath, Action<ImageSource?> setArtworkSource, Action<bool> setIsArtworkLoading)
    {
        if (!File.Exists(filePath))
        {
            ClearArtwork(setArtworkSource, setIsArtworkLoading);
            return;
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
        {
            ClearArtwork(setArtworkSource, setIsArtworkLoading);
            return;
        }

        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            LoadFromFileAndroid(filePath, setArtworkSource, setIsArtworkLoading);
        }
        else
        {
            LoadFromFileOtherPlatforms(filePath, setArtworkSource, setIsArtworkLoading);
        }
    }

    /// <summary>
    /// Loads artwork from file on Android platform.
    /// </summary>
    private void LoadFromFileAndroid(string filePath, Action<ImageSource?> setArtworkSource, Action<bool> setIsArtworkLoading)
    {
        try
        {
            artworkBytes = File.ReadAllBytes(filePath);
            if (artworkBytes == null || artworkBytes.Length == 0)
            {
                ClearArtwork(setArtworkSource, setIsArtworkLoading);
                return;
            }

            // Store bytes in field to keep them alive, create new stream each time
            // Capture for lambda
            var bytes = artworkBytes;
            setArtworkSource(ImageSource.FromStream(() => new MemoryStream(bytes)));
            setIsArtworkLoading(false);
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Failed to create ImageSource from stream for Android, trying FromFile fallback: {FilePath}", filePath);
            artworkBytes = null;
            LoadFromFileFallback(filePath, setArtworkSource, setIsArtworkLoading);
        }
    }

    /// <summary>
    /// Loads artwork from file on non-Android platforms.
    /// </summary>
    private void LoadFromFileOtherPlatforms(string filePath, Action<ImageSource?> setArtworkSource, Action<bool> setIsArtworkLoading)
    {
        try
        {
            setArtworkSource(ImageSource.FromFile(filePath));
            setIsArtworkLoading(false);
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Failed to create ImageSource from file for artwork: {FilePath}", filePath);
            ClearArtwork(setArtworkSource, setIsArtworkLoading);
        }
    }

    /// <summary>
    /// Fallback method for loading artwork from file.
    /// </summary>
    private void LoadFromFileFallback(string filePath, Action<ImageSource?> setArtworkSource, Action<bool> setIsArtworkLoading)
    {
        try
        {
            setArtworkSource(ImageSource.FromFile(filePath));
            setIsArtworkLoading(false);
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Failed to create ImageSource from file for artwork (fallback): {FilePath}", filePath);
            ClearArtwork(setArtworkSource, setIsArtworkLoading);
        }
    }
}
