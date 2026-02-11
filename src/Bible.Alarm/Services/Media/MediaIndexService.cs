using System.IO.Compression;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;
using Serilog;

namespace Bible.Alarm.Services.Media;

public sealed class MediaIndexService(
    ILogger logger,
    IStorageService storageService,
    IMediaIndexVersionService versionService,
    IServiceProvider serviceProvider)
    : IMediaIndexService
{
    private readonly Lazy<string> indexRoot = new(() => storageService.StorageRoot);

    public string IndexRoot => indexRoot.Value;

    private readonly SemaphoreSlim @lock = new(1);
    private static bool verified;

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


    private async Task<bool> IndexDoNotExistOrIsOutdated()
    {
        var tmpIndexFilePath = Path.Combine(IndexRoot, "index.zip");

        var mediaIndexDbExists = await storageService.FileExists(Path.Combine(IndexRoot, "mediaIndex.db"))
                                 //verify that any previous unzipping process was not incomplete
                                 && !await storageService.FileExists(tmpIndexFilePath);

        // If index doesn't exist, copy from embedded resource
        // Media index is now only updated via app updates, not downloaded from AWS
        return !mediaIndexDbExists;
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

        await SafeExtractZipAsync(tmpIndexFilePath, IndexRoot);

        await storageService.DeleteFile(tmpIndexFilePath);
        await versionService.SaveCurrentVersionAsync();
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
