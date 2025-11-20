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
            var creationDate =
                await _storageService.GetFileCreationDate(Path.Combine(IndexRoot, "mediaIndex.db"), false);

            //if downloaded within last 12 hours
            if (creationDate.UtcDateTime > DateTime.UtcNow.AddHours(-AppConstants.CacheSettings.MediaIndexUpdateCheckHours)) return false;

            var time = DateTime.UtcNow;

            for (var i = 0; time.Day > 0 && i <= 1; i++)
            {
                var bytes = await _downloadService.DownloadAsync(
                    $"{AppConstants.ApiEndpoints.MediaIndexDownloadBaseUrl}/{time.Day - i}-{time.Month}-{time.Year}.zip");

                if (bytes == null) continue;

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

                return true;
            }

            return false;
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

    public void Dispose()
    {
        _lock.Dispose();
        // Note: _storageService (IStorageService) is a singleton
        // and should not be disposed here as it is managed by the DI container
    }
}