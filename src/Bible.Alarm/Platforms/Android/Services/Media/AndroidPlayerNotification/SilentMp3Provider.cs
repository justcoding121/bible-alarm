#nullable enable
using Bible.Alarm.Services.Storage.Interfaces;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Media.AndroidPlayerNotification;

/// <summary>
/// Handles silent MP3 file operations for Android player notifications.
/// </summary>
public sealed class SilentMp3Provider(ILogger logger)
{
    /// <summary>
    /// Gets the URI of the silent MP3 file from the storage directory.
    /// The file is copied from embedded resources during bootstrap.
    /// </summary>
    public string? GetSilentMp3Uri()
    {
        try
        {
            var storageService = GetValidatedStorageService();
            if (storageService == null)
            {
                return null;
            }

            var filePath = GetSilentMp3FilePath(storageService);
            if (!File.Exists(filePath))
            {
                logger.Warning("Silent MP3 not found in storage: {FilePath}. It should have been copied during bootstrap.", filePath);
                return null;
            }

            return CreateFileUri(filePath);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting silent MP3 URI");
            return null;
        }
    }

    /// <summary>
    /// Gets and validates the storage service.
    /// </summary>
    private IStorageService? GetValidatedStorageService()
    {
        var storageService = ServiceProviderManager.GetService<IStorageService>();
        if (storageService == null)
        {
            logger.Error("IStorageService not available - cannot get silent MP3 URI");
        }
        return storageService;
    }

    /// <summary>
    /// Gets the file path for the silent MP3 file.
    /// </summary>
    private string GetSilentMp3FilePath(IStorageService storageService)
    {
        const string ResourceFileName = "silent.mp3";
        // Use StorageRoot (same directory as schedule database) instead of CacheRoot
        // because cache can get deleted by the system
        var storageDir = storageService.StorageRoot;
        return Path.Combine(storageDir, ResourceFileName);
    }

    /// <summary>
    /// Creates a file URI from the file path.
    /// </summary>
    private string CreateFileUri(string filePath)
    {
        var uri = new System.Uri(filePath).AbsoluteUri;
        logger.Debug("Using silent MP3 from storage: {FilePath}", filePath);
        return uri;
    }
}
