using Bible.Alarm.Contracts.Platform;
using Bible.Alarm.Services.Contracts;
using NLog;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Controls;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;

namespace Bible.Alarm.Services
{
    public class MediaIndexService : IDisposable
    {
        private static readonly Lazy<Logger> lazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger logger => lazyLogger.Value;

        private readonly Lazy<string> indexRoot;

        private IStorageService storageService;
        private IVersionFinder versionFinder;
        private IDownloadService downloadService;

        public string IndexRoot => indexRoot.Value;

        public MediaIndexService(IStorageService storageService, IVersionFinder versionFinder, IDownloadService downloadService)
        {
            this.storageService = storageService;
            this.versionFinder = versionFinder;
            this.downloadService = downloadService;

            indexRoot = new Lazy<string>(() => this.storageService.StorageRoot);
        }

        private readonly SemaphoreSlim @lock = new SemaphoreSlim(1);
        private static bool verified = false;

        public async Task Verify()
        {
            try
            {
                await @lock.WaitAsync();

                if (verified)
                {
                    return;
                }

                if (await indexDoNotExistOrIsOutdated())
                {
                    await clearCopyIndexFromResource();
                }

                verified = true;
            }
            finally
            {
                try
                {
                    @lock.Release();
                }
                catch (ObjectDisposedException e)
                {
                    logger.Error(e, "MediaIndexService: @lock disposed error.");
                }
            }
        }

        public async Task<bool> UpdateIndexIfAvailable()
        {
            await @lock.WaitAsync();

            try
            {
                var creationDate = await storageService.GetFileCreationDate(Path.Combine(IndexRoot, "mediaIndex.db"), false);

                //if downloaded within last 12 hours
                if (creationDate.UtcDateTime > DateTime.UtcNow.AddHours(-12))
                {
                    return false;
                }

                var time = DateTime.UtcNow;

                for (int i = 0; time.Day > 0 && i <= 1; i++)
                {
                    var bytes = await downloadService.DownloadAsync($"https://jthomas.info/bible-alarm/media-index/{time.Day - i}-{time.Month}-{time.Year}.zip");

                    if (bytes != null)
                    {
                        var indexZipFileName = "index.zip";

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

                        var extractionDir = Path.Combine(IndexRoot, "tmp");
                        await storageService.CreateDirectory(extractionDir);

                        ZipFile.ExtractToDirectory(tmpIndexZipFilePath, extractionDir);

                        File.Copy(Path.Combine(extractionDir, "mediaIndex.db"), Path.Combine(IndexRoot, "mediaIndex.db"), true);

                        await storageService.DeleteDirectory(extractionDir);
                        await storageService.DeleteFile(tmpIndexZipFilePath);

                        return true;
                    }
                }

            }
            finally
            {
                @lock.Release();
            }

            return false;
        }

        private async Task<bool> indexDoNotExistOrIsOutdated()
        {
            var tmpIndexFilePath = Path.Combine(IndexRoot, "index.zip");

            var mediaIndexDbExists = await storageService.FileExists(Path.Combine(IndexRoot, "mediaIndex.db"))
                            //verify that any previous unzipping process was not incomplete
                            && !await storageService.FileExists(tmpIndexFilePath);

            var versionFileExists = false;
            var isOutdatedVersion = false;

            //delete the file if it was outdated by an app auto-update.
            if (mediaIndexDbExists)
            {
                var versionFilePath = Path.Combine(IndexRoot, "version.dat");
                versionFileExists = await storageService.FileExists(versionFilePath);

                if (versionFileExists)
                {
                    var version = await storageService.ReadFile(versionFilePath);
                    var currentVersion = versionFinder.GetVersionName();

                    if (version != currentVersion)
                    {
                        isOutdatedVersion = true;
                    }
                }
            }

            return !mediaIndexDbExists || !versionFileExists || isOutdatedVersion;
        }

        private async Task clearCopyIndexFromResource()
        {
            var indexResourceFile = "index.zip";
            var defaultAlarmFile = "cool-alarm-tone-notification-sound.mp3";

            if (!Directory.Exists(IndexRoot))
            {
                Directory.CreateDirectory(IndexRoot);
            }

            var tmpIndexFilePath = Path.Combine(IndexRoot, indexResourceFile);

            if (await storageService.FileExists(tmpIndexFilePath))
            {
                await storageService.DeleteFile(tmpIndexFilePath);
            }
            if (CurrentDevice.RuntimePlatform == Device.Android &&
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

            if (CurrentDevice.RuntimePlatform == Device.Android)
            {
                await storageService.CopyResourceFile(defaultAlarmFile, IndexRoot, defaultAlarmFile);
            }

            await storageService.DeleteFile(tmpIndexFilePath);
            await storageService.SaveFile(IndexRoot, "version.dat", versionFinder.GetVersionName());
        }

        public void Dispose()
        {
            storageService.Dispose();
            @lock.Dispose();
        }
    }
}
