using System.IO.Compression;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Interfaces.Platform;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class MediaIndexService : IMediaIndexService, IDisposable
{
    private readonly ILogger logger;

    private readonly Lazy<string> indexRoot;

    private readonly IStorageService storageService;
    private readonly IVersionFinder versionFinder;
    private readonly IDownloadService downloadService;

    public string IndexRoot => indexRoot.Value;

    public MediaIndexService(ILogger logger, IStorageService storageService, IVersionFinder versionFinder,
        IDownloadService downloadService)
    {
        this.logger = logger;
        this.storageService = storageService;
        this.versionFinder = versionFinder;
        this.downloadService = downloadService;

        indexRoot = new Lazy<string>(() => storageService.StorageRoot);
    }

    private readonly SemaphoreSlim @lock = new(1);
    private static bool verified;

    public async Task Verify()
    {
        await ConcurrencyHelper.ExecuteAsync(@lock, async () =>
        {
            if (verified)
            {
                return;
            }

            if (await IndexDoNotExistOrIsOutdated())
            {
                await ClearCopyIndexFromResource();
            }

            verified = true;
        }, ex => logger.Error(ex, "MediaIndexService: @lock disposed error."));
    }

    public async Task UpdateMediaIndex()
    {
        await UpdateIndexIfAvailable();
    }

    public async Task<bool> UpdateIndexIfAvailable()
    {
        return await ConcurrencyHelper.ExecuteAsync(@lock, async () =>
        {
            try
            {
                var mediaIndexPath = Path.Combine(IndexRoot, "mediaIndex.db");
                var indexExists = await storageService.FileExists(mediaIndexPath);

                if (!indexExists)
                {
                    logger.Information("Media index does not exist, attempting to download");
                }
                else
                {
                    var creationDate = await storageService.GetFileCreationDate(mediaIndexPath);

                    //if downloaded within last week (harvester runs weekly on Sundays)
                    if (creationDate.UtcDateTime > DateTime.UtcNow.AddDays(-AppConstants.CacheSettings.MediaIndexUpdateCheckDays))
                    {
                        logger.Debug("Media index is up to date (downloaded within last {Days} days)",
                            AppConstants.CacheSettings.MediaIndexUpdateCheckDays);
                        return false;
                    }

                    logger.Information("Media index is older than {Days} days (created: {CreationDate}), attempting to update",
                        AppConstants.CacheSettings.MediaIndexUpdateCheckDays,
                        creationDate.UtcDateTime);
                }

                // Single-attempt policy:
                // We do NOT try previous weeks here because repeated 403/404s can significantly slow bootstrapping
                // (especially for Android Auto/headless flows). If the current week's index is unavailable,
                // we fall back to the existing local index (if any).
                var checkDate = DateTime.UtcNow;
                var url = $"{AppConstants.ApiEndpoints.MediaIndexDownloadBaseUrl}/{AppConstants.ApiEndpoints.MediaIndexFileNamePrefix}{checkDate.Day}-{checkDate.Month}-{checkDate.Year}.zip";
                logger.Debug("Attempting to download media index from: {Url}", url);

                byte[] bytes;
                try
                {
                    bytes = await downloadService.DownloadAsync(url);
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "Failed to download media index from {Url}, using existing index if available", url);
                    return false;
                }

                if (bytes == null || bytes.Length == 0)
                {
                    logger.Warning("Downloaded media index from {Url} is empty, using existing index if available", url);
                    return false;
                }

                const string indexZipFileName = AppConstants.FilePaths.MediaIndexZipFileName;

                if (!Directory.Exists(IndexRoot))
                {
                    Directory.CreateDirectory(IndexRoot);
                }

                var tmpIndexZipFilePath = Path.Combine(IndexRoot, indexZipFileName);

                if (await storageService.FileExists(tmpIndexZipFilePath))
                {
                    await storageService.DeleteFile(tmpIndexZipFilePath);
                }

                await storageService.SaveFile(IndexRoot, indexZipFileName, bytes);

                if (await storageService.FileExists(Path.Combine(IndexRoot, "mediaIndex.db")))
                {
                    await storageService.DeleteFile(Path.Combine(IndexRoot, "mediaIndex.db"));
                }

                var extractionDir = Path.Combine(IndexRoot, AppConstants.FilePaths.TempExtractionDirectoryName);
                await storageService.CreateDirectory(extractionDir);

                ZipFile.ExtractToDirectory(tmpIndexZipFilePath, extractionDir);

                File.Copy(Path.Combine(extractionDir, "mediaIndex.db"), Path.Combine(IndexRoot, "mediaIndex.db"),
                    true);

                await storageService.DeleteDirectory(extractionDir);
                await storageService.DeleteFile(tmpIndexZipFilePath);

                logger.Information("Successfully updated media index from {Url}", url);
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error updating media index, will use existing index if available");
                return false;
            }
        });
    }

    private async Task<bool> IndexDoNotExistOrIsOutdated()
    {
        var tmpIndexFilePath = Path.Combine(IndexRoot, "index.zip");

        var mediaIndexDbExists = await storageService.FileExists(Path.Combine(IndexRoot, "mediaIndex.db"))
                                 //verify that any previous unzipping process was not incomplete
                                 && !await storageService.FileExists(tmpIndexFilePath);

        var isOutdatedVersion = false;

        //delete the file if it was outdated by an app auto-update.
        if (!mediaIndexDbExists)
        {
            return true;
        }

        var versionFilePath = Path.Combine(IndexRoot, "version.dat");
        var versionFileExists = await storageService.FileExists(versionFilePath);

        if (!versionFileExists)
        {
            return true;
        }

        var version = await storageService.ReadFile(versionFilePath);
        var currentVersion = versionFinder.GetVersionName();

        if (version != currentVersion)
        {
            isOutdatedVersion = true;
        }

        return isOutdatedVersion;
    }

    private async Task ClearCopyIndexFromResource()
    {
        const string indexResourceFile = "index.zip";
        const string defaultAlarmFile = "cool-alarm-tone-notification-sound.mp3";

        if (!Directory.Exists(IndexRoot))
        {
            Directory.CreateDirectory(IndexRoot);
        }

        var tmpIndexFilePath = Path.Combine(IndexRoot, indexResourceFile);

        if (await storageService.FileExists(tmpIndexFilePath))
        {
            await storageService.DeleteFile(tmpIndexFilePath);
        }

        if (DeviceInfo.Platform == DevicePlatform.Android &&
            await storageService.FileExists(Path.Combine(IndexRoot, defaultAlarmFile)))
        {
            await storageService.DeleteFile(Path.Combine(IndexRoot, defaultAlarmFile));
        }

        await storageService.CopyResourceFile(indexResourceFile, IndexRoot, indexResourceFile);

        if (await storageService.FileExists(Path.Combine(IndexRoot, "mediaIndex.db")))
        {
            await storageService.DeleteFile(Path.Combine(IndexRoot, "mediaIndex.db"));
        }

        ZipFile.ExtractToDirectory(tmpIndexFilePath, IndexRoot);

        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            await storageService.CopyResourceFile(defaultAlarmFile, IndexRoot, defaultAlarmFile);
        }

        await storageService.DeleteFile(tmpIndexFilePath);
        await storageService.SaveFile(IndexRoot, "version.dat", versionFinder.GetVersionName());
    }

    private bool isDisposed;

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        @lock.Dispose();

        // Note: storageService (IStorageService), versionFinder (IVersionFinder), 
        // and downloadService (IDownloadService) are singletons
        // and should not be disposed here as they are managed by the DI container
    }
}
