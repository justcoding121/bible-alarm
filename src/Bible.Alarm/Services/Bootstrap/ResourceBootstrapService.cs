#nullable enable

using Bible.Alarm.Services.Bootstrap.Interfaces;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Bootstrap;

/// <summary>
/// Service for copying resource files during bootstrap.
/// </summary>
public class ResourceBootstrapService : IResourceBootstrapService
{
    private readonly IMediaIndexService mediaIndexService;
    private readonly IStorageService storageService;

    public ResourceBootstrapService(
        IMediaIndexService mediaIndexService,
        IStorageService storageService)
    {
        this.mediaIndexService = mediaIndexService;
        this.storageService = storageService;
    }

    public async Task CopyResourcesAsync()
    {
        // Verify media index service
        await VerifyMediaLookUpServiceAsync();

        // Copy silent.mp3 for Android Auto
        await CopySilentMp3ToStorageAsync();
    }

    private async Task VerifyMediaLookUpServiceAsync()
    {
        await mediaIndexService.Verify();
    }

    /// <summary>
    /// Copies silent.mp3 from embedded resources to storage directory (same as schedule database).
    /// This ensures the file is available for Android Auto dummy tracks.
    /// Only runs on Android platform. Fast exits if file already exists.
    /// </summary>
    private async Task CopySilentMp3ToStorageAsync()
    {
#if ANDROID
        try
        {
            const string ResourceFileName = "silent.mp3";
            // Copy to StorageRoot (same directory as schedule database) instead of CacheRoot
            // because cache can get deleted by the system
            var storageDir = storageService.StorageRoot;
            var filePath = System.IO.Path.Combine(storageDir, ResourceFileName);

            // Fast exit: Check if file already exists synchronously first
            if (System.IO.File.Exists(filePath))
            {
                Log.Logger.Debug("Silent MP3 already exists in storage: {FilePath}", filePath);
                return;
            }

            // Copy from embedded resource to storage directory
            await storageService.CopyResourceFile(ResourceFileName, storageDir, ResourceFileName);
            Log.Logger.Information("Silent MP3 copied to storage: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex, "Failed to copy silent MP3 to storage - will attempt to copy on-demand");
        }
#else
        // Only needed on Android for Android Auto dummy tracks
        await Task.CompletedTask;
#endif
    }
}

