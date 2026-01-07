using System.IO.Compression;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Interfaces.Platform;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;
using Serilog;

namespace Bible.Alarm.Services.Media;

public sealed class MediaIndexService(
    ILogger logger,
    IStorageService storageService,
    IDownloadService downloadService,
    IMediaIndexVersionService versionService,
    IServiceProvider serviceProvider)
    : IMediaIndexService
{
    private readonly Lazy<string> indexRoot = new(() => storageService.StorageRoot);

    public string IndexRoot => indexRoot.Value;

    private readonly SemaphoreSlim @lock = new(1);
    private static bool verified;
    private static DateTime? appLaunchTime;

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

    public async Task UpdateMediaIndex() => await UpdateIndexIfAvailable();

    public async Task<bool> UpdateIndexIfAvailable()
    {
        return await ConcurrencyHelper.ExecuteAsync(@lock, async () =>
        {
            try
            {
                // Do not update media index during bootstrap - it should only be updated by scheduled jobs
                // Check if bootstrap is complete to prevent updates during app startup
                if (!BootstrapHelper.IsBootstrapCompleted())
                {
                    logger.Debug("Skipping media index update - bootstrap not yet complete. Update will be handled by scheduled job.");
                    return false;
                }

                // Do not update media index on app launch - prevent updates for 5 minutes after app launch
                // This prevents automatic updates when scheduled jobs run immediately after bootstrap
                if (appLaunchTime == null)
                {
                    appLaunchTime = DateTime.UtcNow;
                }

                var timeSinceLaunch = DateTime.UtcNow - appLaunchTime.Value;
                if (timeSinceLaunch.TotalMinutes < 5)
                {
                    logger.Debug("Skipping media index update - app launched {Seconds} seconds ago. Update will be handled by scheduled job after launch period.",
                        timeSinceLaunch.TotalSeconds);
                    return false;
                }

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

                const string IndexZipFileName = AppConstants.FilePaths.MediaIndexZipFileName;

                if (!Directory.Exists(IndexRoot))
                {
                    Directory.CreateDirectory(IndexRoot);
                }

                var tmpIndexZipFilePath = Path.Combine(IndexRoot, IndexZipFileName);

                if (await storageService.FileExists(tmpIndexZipFilePath))
                {
                    await storageService.DeleteFile(tmpIndexZipFilePath);
                }

                await storageService.SaveFile(IndexRoot, IndexZipFileName, bytes);

                var extractionDir = Path.Combine(IndexRoot, AppConstants.FilePaths.TempExtractionDirectoryName);
                await storageService.CreateDirectory(extractionDir);

                ZipFile.ExtractToDirectory(tmpIndexZipFilePath, extractionDir);

                // Use atomic swap pattern to avoid race conditions:
                // 1. Copy new database to temporary name
                // 2. Delete SQLite auxiliary files (WAL, journal, shm) - safe to delete while DB is in use
                // 3. Atomically replace old database with new one using File.Move with overwrite
                var newDbPath = Path.Combine(extractionDir, "mediaIndex.db");
                var tempDbPath = Path.Combine(IndexRoot, "mediaIndex.db.new");
                var finalDbPath = Path.Combine(IndexRoot, "mediaIndex.db");

                // Copy new database to temporary name in final location
                // Use retry policy in case of file locking issues
                await fileOperationRetryPolicy.ExecuteAsync(async () =>
                {
                    File.Copy(newDbPath, tempDbPath, overwrite: true);
                    await Task.CompletedTask;
                });

                // Delete SQLite auxiliary files (WAL mode files) - these can be safely deleted
                // even if the database is in use, as they'll be recreated if needed
                var walPath = finalDbPath + "-wal";
                var shmPath = finalDbPath + "-shm";
                var journalPath = finalDbPath + "-journal";

                if (await storageService.FileExists(walPath))
                {
                    await storageService.DeleteFile(walPath);
                }

                if (await storageService.FileExists(shmPath))
                {
                    await storageService.DeleteFile(shmPath);
                }

                if (await storageService.FileExists(journalPath))
                {
                    await storageService.DeleteFile(journalPath);
                }

                // Atomically replace old database with new one using Polly retry policy
                // File.Move with overwrite=true should atomically replace the file on most platforms
                // This ensures the file always exists - no gap between delete and copy
                // Retry logic handles cases where database connections haven't fully closed yet
                try
                {
                    await fileOperationRetryPolicy.ExecuteAsync(async () =>
                    {
                        File.Move(tempDbPath, finalDbPath, overwrite: true);
                        await Task.CompletedTask;
                    });
                }
                catch (Exception moveEx) when (!(moveEx is IOException || moveEx is UnauthorizedAccessException))
                {
                    // Fallback for platforms where Move with overwrite might not work
                    // Only catch non-locking exceptions (Polly will retry locking exceptions)
                    logger.Warning(moveEx, "File.Move with overwrite failed, using delete+copy fallback");
                    if (await storageService.FileExists(finalDbPath))
                    {
                        await fileOperationRetryPolicy.ExecuteAsync(async () =>
                        {
                            await storageService.DeleteFile(finalDbPath);
                        });
                    }
                    await fileOperationRetryPolicy.ExecuteAsync(async () =>
                    {
                        File.Copy(tempDbPath, finalDbPath, overwrite: true);
                        await Task.CompletedTask;
                    });
                    File.Delete(tempDbPath);
                }

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

        //delete the file if it was outdated by an app auto-update.
        if (!mediaIndexDbExists)
        {
            return true;
        }

        // Check if version is current using version service
        var isVersionCurrent = await versionService.IsVersionCurrentAsync();
        return !isVersionCurrent;
    }

    private async Task ClearCopyIndexFromResource()
    {
        const string IndexResourceFile = "index.zip";

        if (!Directory.Exists(IndexRoot))
        {
            Directory.CreateDirectory(IndexRoot);
        }

        var tmpIndexFilePath = Path.Combine(IndexRoot, IndexResourceFile);

        if (await storageService.FileExists(tmpIndexFilePath))
        {
            await storageService.DeleteFile(tmpIndexFilePath);
        }

        await storageService.CopyResourceFile(IndexResourceFile, IndexRoot, IndexResourceFile);

        var mediaIndexDbPath = Path.Combine(IndexRoot, "mediaIndex.db");
        if (await storageService.FileExists(mediaIndexDbPath))
        {
            // Close MediaDbContext connections specifically to avoid affecting ScheduleDbContext
            // This prevents "file is being used by another process" errors
            CloseMediaDbContextConnections();
            
            // Delete SQLite auxiliary files (WAL mode files) first - these can be safely deleted
            // even if the database is in use, as they'll be recreated if needed
            var walPath = mediaIndexDbPath + "-wal";
            var shmPath = mediaIndexDbPath + "-shm";
            var journalPath = mediaIndexDbPath + "-journal";

            if (await storageService.FileExists(walPath))
            {
                await storageService.DeleteFile(walPath);
            }

            if (await storageService.FileExists(shmPath))
            {
                await storageService.DeleteFile(shmPath);
            }

            if (await storageService.FileExists(journalPath))
            {
                await storageService.DeleteFile(journalPath);
            }

            // Use retry policy to handle cases where database connections haven't fully closed yet
            await fileOperationRetryPolicy.ExecuteAsync(async () =>
            {
                await storageService.DeleteFile(mediaIndexDbPath);
            });
        }

        ZipFile.ExtractToDirectory(tmpIndexFilePath, IndexRoot);

        await storageService.DeleteFile(tmpIndexFilePath);
        await versionService.SaveCurrentVersionAsync();
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

        // Note: storageService (IStorageService) and downloadService (IDownloadService) are singletons
        // and should not be disposed here as they are managed by the DI container
    }
}
