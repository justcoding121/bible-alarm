#nullable enable

using Bible.Alarm.Services.Bootstrap.Interfaces;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Constants;
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
        await mediaIndexService.Verify();

        await CopySilentMp3ToStorageAsync();
    }

    public bool WasMediaIndexReplacedThisRun() => mediaIndexService.WasIndexReplacedThisRun;

    public async Task MigrateNonEnglishMediaDataAsync()
    {
        await mediaIndexService.MigrateNonEnglishDataIfNeededAsync();
    }

    /// <summary>
    /// Copies the silent MP3 from embedded resources to storage (same directory as schedule database).
    /// Uses a versioned filename (e.g. silent_preparing.mp3); when the file is updated, use a new name
    /// so existing installs get the copy. Only runs on Android. Skips copy if file already exists.
    /// Deletes the legacy silent.mp3 from storage if present.
    /// </summary>
    private async Task CopySilentMp3ToStorageAsync()
    {
        GC.KeepAlive(this.mediaIndexService);
        GC.KeepAlive(this.storageService);
#if ANDROID
        try
        {
            var storageDir = storageService.StorageRoot;

            string[] legacyNames =
            [
                AppConstants.FilePaths.SilentMp3LegacyFileName,
                AppConstants.FilePaths.SilentMp3LegacyPreparingFileName,
            ];
            foreach (var legacyName in legacyNames)
            {
                var legacyPath = System.IO.Path.Combine(storageDir, legacyName);
                if (await storageService.FileExists(legacyPath))
                {
                    await storageService.DeleteFile(legacyPath);
                    Log.Logger.Debug("Deleted legacy silent MP3 from storage: {FilePath}", legacyPath);
                }
            }

            var resourceFileName = AppConstants.FilePaths.SilentMp3FileName;
            var filePath = System.IO.Path.Combine(storageDir, resourceFileName);

            if (System.IO.File.Exists(filePath))
            {
                Log.Logger.Debug("Silent MP3 already exists in storage: {FilePath}", filePath);
                return;
            }

            await storageService.CopyResourceFile(resourceFileName, storageDir, resourceFileName);
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

