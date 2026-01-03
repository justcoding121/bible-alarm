#nullable enable
using Bible;
using Serilog;

namespace Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;

/// <summary>
/// Handles artwork loading for AlarmViewModal.
/// Separated from AlarmViewModal for better modularity.
/// </summary>
public class AlarmViewModelArtworkHandler
{
    private readonly ILogger logger;
    private readonly Action<ImageSource?> setArtworkSource;
    private readonly Action<bool> setIsArtworkLoading;
    private readonly Action clearArtworkBytes;

    private string? lastArtworkUrl;

    public AlarmViewModelArtworkHandler(
        ILogger logger,
        Action<ImageSource?> setArtworkSource,
        Action<bool> setIsArtworkLoading,
        Action clearArtworkBytes)
    {
        this.logger = logger;
        this.setArtworkSource = setArtworkSource;
        this.setIsArtworkLoading = setIsArtworkLoading;
        this.clearArtworkBytes = clearArtworkBytes;
    }

    public void UpdateArtwork(string? artworkUrl, Func<string, bool> tryLoadFromUri, Func<string, string?> resolveFilePath, Action<string> loadFromFile, Action clearArtwork)
    {
        // Avoid unnecessary updates if URL hasn't changed
        if (lastArtworkUrl == artworkUrl)
        {
            return;
        }

        var previousUrl = lastArtworkUrl;
        lastArtworkUrl = artworkUrl;

        if (string.IsNullOrEmpty(artworkUrl))
        {
            clearArtwork();
            return;
        }

        // Only clear existing artwork if we're switching to a different artwork
        if (!string.IsNullOrEmpty(previousUrl) && previousUrl != artworkUrl)
        {
            clearArtwork();
        }

        setIsArtworkLoading(true);

        try
        {
            if (tryLoadFromUri(artworkUrl))
            {
                return;
            }

            var filePath = resolveFilePath(artworkUrl);
            if (!string.IsNullOrEmpty(filePath))
            {
                loadFromFile(filePath);
            }
            else
            {
                clearArtwork();
            }
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Error updating artwork from URL: {ArtworkUrl}", artworkUrl);
            clearArtwork();
        }
    }

    public void ClearArtwork()
    {
        setArtworkSource(null);
        clearArtworkBytes();
    }
}

