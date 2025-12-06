using System.IO.Compression;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Interfaces.Platform;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class MediaIndexService : IMediaIndexService, IDisposable
{
    private readonly ILogger _logger;

    private readonly Lazy<string> _indexRoot;

    private readonly IStorageService _storageService;
    private readonly IVersionFinder _versionFinder;
    private readonly IDownloadService _downloadService;

    public string IndexRoot => _indexRoot.Value;

    public MediaIndexService(ILogger logger, IStorageService storageService, IVersionFinder versionFinder,
        IDownloadService downloadService)
    {
        _logger = logger;
        _storageService = storageService;
        _versionFinder = versionFinder;
        _downloadService = downloadService;

        _indexRoot = new Lazy<string>(() => _storageService.StorageRoot);
    }

    private readonly SemaphoreSlim _lock = new(1);
    private static bool verified;

    public async Task Verify()
    {
        await ConcurrencyHelper.ExecuteAsync(_lock, async () =>
        {
            if (verified) return;

            if (await IndexDoNotExistOrIsOutdated()) await ClearCopyIndexFromResource();

            verified = true;
        }, ex => _logger.Error(ex, "MediaIndexService: @lock disposed error."));
    }

    public async Task UpdateMediaIndex()
    {
        await UpdateIndexIfAvailable();
    }

    public async Task<bool> UpdateIndexIfAvailable()
    {
        return await ConcurrencyHelper.ExecuteAsync(_lock, async () =>
        {
            try
            {
                var mediaIndexPath = Path.Combine(IndexRoot, "mediaIndex.db");
                var indexExists = await _storageService.FileExists(mediaIndexPath);
                
                if (!indexExists)
                {
                    _logger.Information("Media index does not exist, attempting to download");
                }
                else
                {
                    var creationDate = await _storageService.GetFileCreationDate(mediaIndexPath, false);

                    //if downloaded within last week (harvester runs weekly on Sundays)
                    if (creationDate.UtcDateTime > DateTime.UtcNow.AddDays(-AppConstants.CacheSettings.MediaIndexUpdateCheckDays))
                    {
                        _logger.Debug("Media index is up to date (downloaded within last {Days} days)", 
                            AppConstants.CacheSettings.MediaIndexUpdateCheckDays);
                        return false;
                    }

                    _logger.Information("Media index is older than {Days} days (created: {CreationDate}), attempting to update", 
                        AppConstants.CacheSettings.MediaIndexUpdateCheckDays,
                        creationDate.UtcDateTime);
                }

                // Try to find the most recent weekly index file
                // Harvester runs weekly on Sundays, so we check the last few weeks
                var time = DateTime.UtcNow;
                // Check up to 4 weeks back to find the latest index
                var weeksToCheck = 4;

                for (var i = 0; i < weeksToCheck; i++)
                {
                    var checkDate = time.AddDays(-i * 7);
                    var url = $"{AppConstants.ApiEndpoints.MediaIndexDownloadBaseUrl}/{checkDate.Day}-{checkDate.Month}-{checkDate.Year}.zip";
                    _logger.Debug("Attempting to download media index from: {Url} (week {WeekNumber} ago)", url, i);
                    
                    byte[] bytes;
                    try
                    {
                        bytes = await _downloadService.DownloadAsync(url);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Failed to download media index from {Url}, will try previous week if available", url);
                        continue;
                    }

                    if (bytes == null || bytes.Length == 0)
                    {
                        _logger.Warning("Downloaded media index from {Url} is empty, trying previous week", url);
                        continue;
                    }

                    const string indexZipFileName = AppConstants.FilePaths.MediaIndexZipFileName;

                    if (!Directory.Exists(IndexRoot)) Directory.CreateDirectory(IndexRoot);

                    var tmpIndexZipFilePath = Path.Combine(IndexRoot, indexZipFileName);

                    if (await _storageService.FileExists(tmpIndexZipFilePath))
                        await _storageService.DeleteFile(tmpIndexZipFilePath);

                    await _storageService.SaveFile(IndexRoot, indexZipFileName, bytes);

                    if (await _storageService.FileExists(Path.Combine(IndexRoot, "mediaIndex.db")))
                        await _storageService.DeleteFile(Path.Combine(IndexRoot, "mediaIndex.db"));

                    var extractionDir = Path.Combine(IndexRoot, AppConstants.FilePaths.TempExtractionDirectoryName);
                    await _storageService.CreateDirectory(extractionDir);

                    ZipFile.ExtractToDirectory(tmpIndexZipFilePath, extractionDir);

                    File.Copy(Path.Combine(extractionDir, "mediaIndex.db"), Path.Combine(IndexRoot, "mediaIndex.db"),
                        true);

                    await _storageService.DeleteDirectory(extractionDir);
                    await _storageService.DeleteFile(tmpIndexZipFilePath);

                    _logger.Information("Successfully updated media index from {Url}", url);
                    return true;
                }

                _logger.Warning("Failed to download media index after checking {WeeksToCheck} weeks, using existing index if available", weeksToCheck);
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating media index, will use existing index if available");
                return false;
            }
        });
    }

    private async Task<bool> IndexDoNotExistOrIsOutdated()
    {
        var tmpIndexFilePath = Path.Combine(IndexRoot, "index.zip");

        var mediaIndexDbExists = await _storageService.FileExists(Path.Combine(IndexRoot, "mediaIndex.db"))
                                 //verify that any previous unzipping process was not incomplete
                                 && !await _storageService.FileExists(tmpIndexFilePath);

        var isOutdatedVersion = false;

        //delete the file if it was outdated by an app auto-update.
        if (!mediaIndexDbExists) return true;
        var versionFilePath = Path.Combine(IndexRoot, "version.dat");
        var versionFileExists = await _storageService.FileExists(versionFilePath);

        if (!versionFileExists) return true;
        var version = await _storageService.ReadFile(versionFilePath);
        var currentVersion = _versionFinder.GetVersionName();

        if (version != currentVersion) isOutdatedVersion = true;

        return isOutdatedVersion;
    }

    private async Task ClearCopyIndexFromResource()
    {
        const string indexResourceFile = "index.zip";
        const string defaultAlarmFile = "cool-alarm-tone-notification-sound.mp3";

        if (!Directory.Exists(IndexRoot)) Directory.CreateDirectory(IndexRoot);

        var tmpIndexFilePath = Path.Combine(IndexRoot, indexResourceFile);

        if (await _storageService.FileExists(tmpIndexFilePath)) await _storageService.DeleteFile(tmpIndexFilePath);
        if (DeviceInfo.Platform == DevicePlatform.Android &&
            await _storageService.FileExists(Path.Combine(IndexRoot, defaultAlarmFile)))
            await _storageService.DeleteFile(Path.Combine(IndexRoot, defaultAlarmFile));

        await _storageService.CopyResourceFile(indexResourceFile, IndexRoot, indexResourceFile);

        if (await _storageService.FileExists(Path.Combine(IndexRoot, "mediaIndex.db")))
            await _storageService.DeleteFile(Path.Combine(IndexRoot, "mediaIndex.db"));

        ZipFile.ExtractToDirectory(tmpIndexFilePath, IndexRoot);

        if (DeviceInfo.Platform == DevicePlatform.Android)
            await _storageService.CopyResourceFile(defaultAlarmFile, IndexRoot, defaultAlarmFile);

        await _storageService.DeleteFile(tmpIndexFilePath);
        await _storageService.SaveFile(IndexRoot, "version.dat", _versionFinder.GetVersionName());
    }

    private bool _isDisposed;
    
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        
        _isDisposed = true;
        
        _lock.Dispose();
        
        // Note: _storageService (IStorageService), _versionFinder (IVersionFinder), 
        // and _downloadService (IDownloadService) are singletons
        // and should not be disposed here as they are managed by the DI container
    }
}