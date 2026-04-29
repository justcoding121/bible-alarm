using System.IO.Compression;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.MediaIndexServiceHelpers;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Retry;
using Serilog;

namespace Bible.Alarm.Services.Media;

public sealed class MediaIndexService(
    ILogger logger,
    IStorageService storageService,
    IMediaIndexVersionService versionService,
    IServiceProvider serviceProvider,
    ILanguageContentService languageContentService)
    : IMediaIndexService
{
    private readonly Lazy<string> indexRoot = new(() => storageService.StorageRoot);

    public string IndexRoot => indexRoot.Value;

    private readonly SemaphoreSlim @lock = new(1);
    private static bool verified;
    private static bool wasIndexReplacedThisRun;

    // Polly retry policy for file operations that may fail due to file locking
    // Retries with exponential backoff to handle cases where database connections haven't fully closed
    private readonly AsyncRetryPolicy fileOperationRetryPolicy = Policy
        .Handle<IOException>() // File locked, access denied, etc.
        .Or<UnauthorizedAccessException>() // Permission denied
        .WaitAndRetryAsync(
            retryCount: 5,
            sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(100 * Math.Pow(2, retryAttempt - 1)), // 100ms, 200ms, 400ms, 800ms, 1600ms
            onRetry: (exception, timespan, retryCount, _) =>
            {
                Log.Logger.Warning(exception, "File operation failed (likely locked), retrying (attempt {RetryCount}/5) after {DelayMs}ms",
                    retryCount, timespan.TotalMilliseconds);
            });

    /// <summary>
    /// True when the media index was replaced this bootstrap run (version change or recovery).
    /// Used to run non-English fetch only on version change, not every launch.
    /// </summary>
    public bool WasIndexReplacedThisRun => wasIndexReplacedThisRun;

    public async Task Verify()
    {
        await ConcurrencyHelper.ExecuteAsync(@lock, async () =>
        {
            if (verified)
            {
                return;
            }

            wasIndexReplacedThisRun = false;
            if (await IndexDoNotExistOrIsOutdated())
            {
                await ClearCopyIndexFromResource();
            }

            verified = true;
        }, ex => logger.Error(ex, "MediaIndexService: @lock disposed error."));
    }


    /// <summary>
    /// Returns true if the media index should be replaced: fresh install, incomplete extraction,
    /// or app version changed (Preferences version != current app version).
    /// Same logic on all platforms (Android, iOS, Windows).
    /// </summary>
    private async Task<bool> IndexDoNotExistOrIsOutdated()
    {
        var tmpIndexFilePath = Path.Combine(IndexRoot, AppConstants.FilePaths.MediaIndexZipFileName);
        var mediaIndexDbPath = Path.Combine(IndexRoot, AppConstants.Database.MediaIndexDatabaseFileName);

        var mediaIndexDbExists = await storageService.FileExists(mediaIndexDbPath)
                                 && !await storageService.FileExists(tmpIndexFilePath);

        if (!mediaIndexDbExists)
        {
            return true;
        }

        if (!await versionService.IsVersionCurrentAsync())
        {
            return true;
        }

        return false;
    }

    private async Task ClearCopyIndexFromResource()
    {
        var indexZipFileName = AppConstants.FilePaths.MediaIndexZipFileName;

        if (!Directory.Exists(IndexRoot))
        {
            Directory.CreateDirectory(IndexRoot);
        }

        var tmpIndexFilePath = Path.Combine(IndexRoot, indexZipFileName);

        if (await storageService.FileExists(tmpIndexFilePath))
        {
            await storageService.DeleteFile(tmpIndexFilePath);
        }

        await storageService.CopyResourceFile(indexZipFileName, IndexRoot, indexZipFileName);

        var mediaIndexDbPath = Path.Combine(IndexRoot, AppConstants.Database.MediaIndexDatabaseFileName);
        var oldMediaIndexDbPath = GetOldMediaIndexPath();

        if (await storageService.FileExists(mediaIndexDbPath))
        {
            wasIndexReplacedThisRun = true;
            CloseMediaDbContextConnections();

            // Rename old DB so MigrateNonEnglishDataIfNeededAsync can copy data from it before deleting.
            // Auxiliary files are renamed alongside the main DB so SQLite can recover WAL data.
            await fileOperationRetryPolicy.ExecuteAsync(async () =>
            {
                File.Move(mediaIndexDbPath, oldMediaIndexDbPath, overwrite: true);
                await Task.CompletedTask;
            });

            RenameAuxiliaryFiles(mediaIndexDbPath, oldMediaIndexDbPath);
        }

        await SafeExtractZipAsync(tmpIndexFilePath, IndexRoot);

        await storageService.DeleteFile(tmpIndexFilePath);
        await versionService.SaveCurrentVersionAsync();
    }

    /// <summary>
    /// Runs only on version change (new media index was just copied and overwritten).
    /// 1. Copy critical refs from old DB (primary, no network needed).
    /// 2. API fetch fallback for remaining critical refs.
    /// 3. Orphan cleanup (safety net).
    /// 4. Fire-and-forget background copy of remaining sections/tracks, then delete old DB.
    /// </summary>
    public async Task MigrateNonEnglishDataIfNeededAsync()
    {
        var oldMediaIndexDbPath = GetOldMediaIndexPath();
        if (!File.Exists(oldMediaIndexDbPath))
        {
            return;
        }

        var newMediaIndexDbPath = Path.Combine(IndexRoot, AppConstants.Database.MediaIndexDatabaseFileName);
        var scheduleDbPath = Path.Combine(IndexRoot, AppConstants.Database.ScheduleDatabaseFileName);

        try
        {
            var copier = new OldMediaIndexDataCopier(logger);
            await copier.CopyMissingAsync(oldMediaIndexDbPath, newMediaIndexDbPath, scheduleDbPath);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Old media index data copy failed (partially or fully)");
        }

        try
        {
            var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var fetcher = new ScheduleMediaBootstrapFetcher(logger, languageContentService, scopeFactory);
            await fetcher.FetchMissingAsync(scheduleDbPath, newMediaIndexDbPath);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Schedule media bootstrap fetch failed (partially or fully)");
        }

        try
        {
            var cleanup = new OrphanedScheduleCleanup(logger);
            await cleanup.CleanupAsync(newMediaIndexDbPath, scheduleDbPath);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to cleanup orphaned schedules");
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var bgCopier = new OldMediaIndexBackgroundCopier(logger);
                await bgCopier.CopyRemainingDataAsync(oldMediaIndexDbPath, newMediaIndexDbPath, scheduleDbPath);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Background copy of remaining media data failed");
            }
            finally
            {
                CleanupOldMediaIndex();
            }
        });
    }

    private string GetOldMediaIndexPath()
    {
        return Path.Combine(IndexRoot,
            Path.GetFileNameWithoutExtension(AppConstants.Database.MediaIndexDatabaseFileName)
            + AppConstants.Database.MediaIndexDatabaseRenamedSuffix
            + Path.GetExtension(AppConstants.Database.MediaIndexDatabaseFileName));
    }

    private void CleanupOldMediaIndex()
    {
        var oldDbPath = GetOldMediaIndexPath();
        try
        {
            DeleteFileIfExists(oldDbPath);
            DeleteFileIfExists(oldDbPath + "-wal");
            DeleteFileIfExists(oldDbPath + "-shm");
            DeleteFileIfExists(oldDbPath + "-journal");
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to clean up old media index files");
        }
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void RenameAuxiliaryFiles(string sourcePath, string destPath)
    {
        string[] suffixes = ["-wal", "-shm", "-journal"];
        foreach (var suffix in suffixes)
        {
            var sourceAux = sourcePath + suffix;
            if (File.Exists(sourceAux))
            {
                var destAux = destPath + suffix;
                File.Move(sourceAux, destAux, overwrite: true);
            }
        }
    }

    /// <summary>
    /// Extracts a zip archive with path and size limits to mitigate zip bombs and path traversal (S5042).
    /// Extracts entry-by-entry to control resource consumption.
    /// </summary>
    private static async Task SafeExtractZipAsync(string zipPath, string destinationDir)
    {
        const long MaxTotalUncompressedBytes = 500 * 1024 * 1024; // 500 MB
        const int MaxEntryCount = 100_000;

        var destinationFullPath = Path.GetFullPath(destinationDir);

        await Task.Run(() =>
        {
            using var archive = ZipFile.OpenRead(zipPath);
            long totalExtracted = 0;
            var entryCount = 0;

            foreach (var entry in archive.Entries)
            {
                entryCount++;
                if (entryCount > MaxEntryCount)
                {
                    throw new InvalidOperationException($"Zip entry count exceeds limit ({MaxEntryCount}).");
                }

                var fullPath = Path.GetFullPath(Path.Combine(destinationFullPath, entry.FullName));
                if (!fullPath.StartsWith(destinationFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Zip entry path traversal detected: {entry.FullName}");
                }

                if (entry.FullName.EndsWith('/'))
                {
                    Directory.CreateDirectory(fullPath);
                    continue;
                }

                var parentDir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                using (var entryStream = entry.Open())
                using (var fileStream = File.Create(fullPath))
                {
                    var buffer = new byte[81920];
                    int read;
                    while ((read = entryStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        totalExtracted += read;
                        if (totalExtracted > MaxTotalUncompressedBytes)
                        {
                            throw new InvalidOperationException($"Zip uncompressed size exceeds limit ({MaxTotalUncompressedBytes} bytes).");
                        }

                        fileStream.Write(buffer, 0, read);
                    }
                }
            }
        });
    }

    /// <summary>
    /// Closes all open MediaDbContext connections to allow database file deletion.
    /// This only affects MediaDbContext connections, not ScheduleDbContext.
    /// </summary>
    private void CloseMediaDbContextConnections()
    {
        try
        {
            // Create a scope to get MediaDbContext
            using var scope = serviceProvider.CreateScope();
            var mediaDbContext = scope.ServiceProvider.GetService<MediaDbContext>();

            if (mediaDbContext != null)
            {
                // Close the database connection explicitly
                var connection = mediaDbContext.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Closed)
                {
                    connection.Close();
                    logger.Debug("Closed MediaDbContext connection to allow database file deletion");
                }
            }

            // Note: We don't call ClearAllPools() here to avoid affecting ScheduleDbContext connections.
            // The explicit connection.Close() above should be sufficient, and the retry policy will handle
            // any remaining file locks. If needed, ClearAllPools() is called as a last resort in the catch block.
        }
        catch (Exception ex)
        {
            // If we can't close connections gracefully, fall back to clearing all pools as last resort
            // This is a trade-off: we affect ScheduleDbContext, but it's better than failing to delete the file
            logger.Warning(ex, "Failed to close MediaDbContext connections gracefully, using ClearAllPools() as last resort");
            SqliteConnection.ClearAllPools();
        }
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

        // Note: storageService (IStorageService) is a singleton
        // and should not be disposed here as it is managed by the DI container
    }
}
