#nullable enable
using Serilog;

namespace Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;

/// <summary>
/// Handles artwork loading and management for the alarm modal.
/// </summary>
public sealed class ArtworkManager(ILogger logger)
{
    private string? lastArtworkUrl;
    private ImageSource? artworkSource;


    /// <summary>
    /// Updates the artwork from a URL. If loading fails (e.g. network or file missing), tries fallbackUrl (e.g. default schedule/Bible Alarm icon) so the spinner does not run forever.
    /// </summary>
    public void UpdateArtwork(string? artworkUrl, Action<ImageSource?> setArtworkSource, Action<bool> setIsArtworkLoading, bool forceReload = false, string? fallbackUrl = null)
    {
        if (lastArtworkUrl == artworkUrl && (!forceReload || artworkSource != null))
        {
            setIsArtworkLoading(false);
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
            else
            {
                setIsArtworkLoading(false);
            }
            return;
        }

        // Clear existing artwork if URL changed or if forcing reload (for same file path with new content)
        if (artworkSource != null && (!string.IsNullOrEmpty(previousUrl) && previousUrl != artworkUrl || forceReload))
        {
            // When forcing reload, preserve the URL tracking
            if (forceReload && previousUrl == artworkUrl)
            {
                artworkSource = null;
                setArtworkSource(null);
                setIsArtworkLoading(false);
            }
            else
            {
                ClearArtwork(setArtworkSource, setIsArtworkLoading);
            }
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
                if (LoadFromFile(filePath, setArtworkSource, setIsArtworkLoading))
                {
                    return;
                }
            }

            TryFallbackArtwork(fallbackUrl, setArtworkSource, setIsArtworkLoading);
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Error updating artwork from URL: {ArtworkUrl}", artworkUrl);
            TryFallbackArtwork(fallbackUrl, setArtworkSource, setIsArtworkLoading);
        }
    }

    private void TryFallbackArtwork(string? fallbackUrl, Action<ImageSource?> setArtworkSource, Action<bool> setIsArtworkLoading)
    {
        if (string.IsNullOrEmpty(fallbackUrl) || fallbackUrl == lastArtworkUrl)
        {
            ClearArtwork(setArtworkSource, setIsArtworkLoading);
            return;
        }
        try
        {
            if (TryLoadFromUri(fallbackUrl, setArtworkSource, setIsArtworkLoading))
            {
                return;
            }
            var filePath = ResolveFilePath(fallbackUrl);
            if (!string.IsNullOrEmpty(filePath) && LoadFromFile(filePath, setArtworkSource, setIsArtworkLoading))
            {
                return;
            }
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Fallback artwork failed: {FallbackUrl}", fallbackUrl);
        }
        ClearArtwork(setArtworkSource, setIsArtworkLoading);
    }

    /// <summary>
    /// Gets whether artwork is currently loaded.
    /// </summary>
    public bool HasArtwork => artworkSource != null;

    /// <summary>
    /// Clears the current artwork.
    /// </summary>
    private void ClearArtwork(Action<ImageSource?> setArtworkSource, Action<bool> setIsArtworkLoading)
    {
        artworkSource = null;
        setArtworkSource(null);
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

        artworkSource = ImageSource.FromUri(uri);
        setArtworkSource(artworkSource);
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
    /// Loads artwork from a file path using FileImageSource so the native platform loads
    /// directly from disk (BitmapFactory on Android, UIImage on iOS) instead of going
    /// through a MemoryStream which MAUI's Image handler can fail to render reliably.
    /// </summary>
    private bool LoadFromFile(string filePath, Action<ImageSource?> setArtworkSource, Action<bool> setIsArtworkLoading)
    {
        if (!File.Exists(filePath))
        {
            logger.Debug("Artwork file not found: {FilePath}", filePath);
            return false;
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
        {
            return false;
        }

        try
        {
            artworkSource = ImageSource.FromFile(filePath);
            setArtworkSource(artworkSource);
            setIsArtworkLoading(false);
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Failed to load artwork from file: {FilePath}", filePath);
            artworkSource = null;
        }

        return artworkSource != null;
    }

}
