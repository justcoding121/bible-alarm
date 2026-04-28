#nullable enable

using Bible.Alarm.Services.Bootstrap.Interfaces;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Services.Bootstrap;

/// <summary>
/// Service for initializing and migrating databases during bootstrap.
/// </summary>
public class DatabaseBootstrapService : IDatabaseBootstrapService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IScheduleDatabaseVersionService scheduleVersionService;
    private readonly IStorageService storageService;

    public DatabaseBootstrapService(
        IServiceScopeFactory scopeFactory,
        IScheduleDatabaseVersionService scheduleVersionService,
        IStorageService storageService)
    {
        this.scopeFactory = scopeFactory;
        this.scheduleVersionService = scheduleVersionService;
        this.storageService = storageService;
    }

    public async Task InitializeAsync()
    {
#if DEBUG
        var dbInitStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
        Log.Logger.Information("[BOOTSTRAP] Database initialization starting");
#endif

        // Create a scope for the DbContext since it's registered as scoped
        // This ensures proper lifetime management and prevents disposal issues
        await using var scope = scopeFactory.CreateAsyncScope();

        // Migrate Schedule database (always safe - app owns this DB)
        // On this release, we're switching to a new database name (schedule.db)
        // Delete old database and version.dat files if they exist (no longer used)
#if DEBUG
        var scheduleDbStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
        var scheduleDb = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        // Get database directory and storage root for cleanup
        var dbPath = scheduleDb.Database.GetDbConnection().DataSource;
        var dbDirectory = System.IO.Path.GetDirectoryName(dbPath) ?? "";

        // Delete only legacy-named database files (never the current schedule DB file if it exists).
        // Recovery path below may delete the current file when migration fails due to corruption.
        var oldDbPath1 = System.IO.Path.Combine(dbDirectory, "bibleAlarm.db");
        await DeleteOldDatabaseFilesAsync(oldDbPath1, "old Schedule database (bibleAlarm.db)");
        
        var oldDbPath2 = System.IO.Path.Combine(dbDirectory, "bibleAlarm2.db");
        await DeleteOldDatabaseFilesAsync(oldDbPath2, "old Schedule database (bibleAlarm2.db)");

        // version.dat files are NOT deleted here — MediaIndexVersionService still uses them as
        // a fallback when Preferences are unavailable (e.g. iOS evicted NSUserDefaults).
        // Deleting them here would race with the parallel MediaIndexService.Verify() call and
        // could cause the media index to be re-extracted unnecessarily, triggering orphan cleanup
        // that deletes user schedules.

        // Check if new database file exists
        var dbExists = System.IO.File.Exists(dbPath);

        // If database doesn't exist, try copying from bundled resource first
        // This eliminates the need for migrations on clean install (saves ~1.2 seconds)
        if (!dbExists)
        {
            await CopyScheduleDatabaseFromResourceIfNeededAsync(dbPath);
            dbExists = System.IO.File.Exists(dbPath); // Re-check after copy attempt
        }

        // Check if version matches (using Preferences only, no version.dat fallback)
        // Even if version matches, we still need to verify the schema exists
        // (database file might be corrupted, empty, or missing schema)
        // GetPendingMigrationsAsync() is fast if schema exists (just reads migrations history table)
        var versionMatches = dbExists && await scheduleVersionService.IsVersionCurrentAsync();

        if (versionMatches)
        {
            Log.Logger.Debug("[BOOTSTRAP] Schedule database version matches current app version, verifying schema...");
        }
        else
        {
            // Version mismatch, first launch, or database doesn't exist
            if (!dbExists)
            {
                Log.Logger.Debug("[BOOTSTRAP] Schedule database file does not exist, will be created from bundled resource or migrations");
            }
            else
            {
                Log.Logger.Debug("[BOOTSTRAP] Schedule database version mismatch or not set, will verify schema and apply migrations if needed");
            }
        }

        // Always verify schema by checking for pending migrations
        // This is fast if schema exists (just reads migrations history table)
        // If bundled database was copied correctly, this should return empty (schema already exists)
        // If bundled database is missing/empty/corrupted, this will return all migrations (need to create schema)
        try
        {
            var pendingScheduleMigrations = await scheduleDb.Database.GetPendingMigrationsAsync();
            if (pendingScheduleMigrations.Any())
            {
                Log.Logger.Information(
                    "[BOOTSTRAP] Schedule database has {Count} pending migrations, applying...",
                    pendingScheduleMigrations.Count());
                await scheduleDb.Database.MigrateAsync();
                Log.Logger.Information("[BOOTSTRAP] Schedule database migrations applied successfully");
            }
            else
            {
                if (versionMatches)
                {
                    Log.Logger.Debug("[BOOTSTRAP] Schedule database schema verified - version matches and all migrations applied");
                }
                else
                {
                    Log.Logger.Debug("[BOOTSTRAP] Schedule database is already up to date (bundled database had schema), skipping migration");
                }
            }
        }
        catch (Exception ex)
        {
            // If GetPendingMigrationsAsync or MigrateAsync fails (e.g., database is corrupted, EF version incompatibility),
            // try to recover by copying the bundled database as a safety net
            Log.Logger.Warning(ex, "[BOOTSTRAP] Failed to check/apply migrations, attempting recovery with bundled database");

            try
            {
                // Close and dispose the database connection before attempting to replace the file
                // This ensures the file is not locked when we try to delete it
                await scheduleDb.Database.CloseConnectionAsync();
                await scheduleDb.DisposeAsync();
                scheduleDb = null!; // Clear reference to help GC

                // Force garbage collection to ensure connection is fully released
                // SQLite connections can hold file locks even after Dispose()
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                // Delete the corrupted database file and any WAL/SHM files with retry logic
                // SQLite may take a moment to fully release the file lock
                if (System.IO.File.Exists(dbPath))
                {
                    const int maxRetries = 5;
                    const int retryDelayMs = 100;
                    bool deleted = false;

                    for (int attempt = 0; attempt < maxRetries && !deleted; attempt++)
                    {
                        try
                        {
                            System.IO.File.Delete(dbPath);
                            deleted = true;
                            Log.Logger.Information("[BOOTSTRAP] Deleted corrupted Schedule database file");
                        }
                        catch (IOException deleteEx) when (attempt < maxRetries - 1)
                        {
                            Log.Logger.Debug(deleteEx,
                                "[BOOTSTRAP] Could not delete corrupted database file (attempt {Attempt}/{MaxRetries}), retrying...",
                                attempt + 1, maxRetries);
                            await Task.Delay(retryDelayMs);
                        }
                        catch (IOException deleteEx)
                        {
                            Log.Logger.Warning(deleteEx,
                                "[BOOTSTRAP] Could not delete corrupted database file after {MaxRetries} attempts, may be locked",
                                maxRetries);
                            throw; // Re-throw on final attempt
                        }
                    }
                }

                // Clean up WAL and SHM files if they exist (these are usually easier to delete)
                var walPath = dbPath + "-wal";
                var shmPath = dbPath + "-shm";
                TryDeleteAuxiliaryDbFile(walPath);
                TryDeleteAuxiliaryDbFile(shmPath);

                // Force overwrite with bundled database (no conditional - always copy)
                await CopyScheduleDatabaseFromResourceForceAsync(dbPath);

                // Verify the database was copied successfully
                if (!System.IO.File.Exists(dbPath))
                {
                    throw new InvalidOperationException("Failed to copy bundled database during recovery");
                }

                // Get a fresh DbContext instance for the new database
                scheduleDb = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

                // Try to verify/apply migrations again with the fresh database
                var pendingScheduleMigrationsAfterRecovery = await scheduleDb.Database.GetPendingMigrationsAsync();
                if (pendingScheduleMigrationsAfterRecovery.Any())
                {
                    Log.Logger.Information(
                        "[BOOTSTRAP] Schedule database recovery: {Count} pending migrations after copying bundled database, applying...",
                        pendingScheduleMigrationsAfterRecovery.Count());
                    await scheduleDb.Database.MigrateAsync();
                    Log.Logger.Information("[BOOTSTRAP] Schedule database migrations applied successfully after recovery");
                }
                else
                {
                    Log.Logger.Information("[BOOTSTRAP] Schedule database recovery successful - bundled database had correct schema");
                }
            }
            catch (Exception recoveryEx)
            {
                Log.Logger.Error(recoveryEx, "[BOOTSTRAP] Failed to recover Schedule database using bundled database");
                throw new InvalidOperationException(
                    "Schedule database migration failed and recovery attempt failed. The app cannot continue without a valid database.",
                    recoveryEx);
            }
        }

        // Save current version after successful schema verification/migration
        // This marks the database as verified for the current app version
        if (!versionMatches)
        {
            await scheduleVersionService.SaveCurrentVersionAsync();
        }
#if DEBUG
        var scheduleDbElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - scheduleDbStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        Log.Logger.Information("[BOOTSTRAP] Schedule database migration completed in {ElapsedMs:F2}ms", scheduleDbElapsed);

        var dbInitElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - dbInitStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        Log.Logger.Information("[BOOTSTRAP] Database initialization completed in {ElapsedMs:F2}ms", dbInitElapsed);
#endif
    }

    private async Task CopyScheduleDatabaseFromResourceIfNeededAsync(string dbPath)
    {
        var dbExists = System.IO.File.Exists(dbPath);

        // If database doesn't exist, copy from bundled resource
        if (!dbExists)
        {
            // Use the same filename as the database file for consistency
            var scheduleDbResourceFile = AppConstants.Database.ScheduleDatabaseFileName;
            var dbDirectory = System.IO.Path.GetDirectoryName(dbPath);

            if (!string.IsNullOrEmpty(dbDirectory) && !System.IO.Directory.Exists(dbDirectory))
            {
                System.IO.Directory.CreateDirectory(dbDirectory);
            }

            try
            {
                // Copy bundled empty database from resources
                await storageService.CopyResourceFile(
                    scheduleDbResourceFile,
                    dbDirectory ?? storageService.StorageRoot,
                    System.IO.Path.GetFileName(dbPath));

                Log.Logger.Information("[BOOTSTRAP] Copied Schedule database from bundled resource");

                // Don't save version here - let migration check verify the database schema is correct
                // The migration check will be fast if schema exists, and will create it if missing
            }
            catch (Exception ex)
            {
                // If resource copy fails (e.g., resource not found), fall back to migrations
                Log.Logger.Debug(ex,
                    "Failed to copy Schedule database from resource, will create with migrations instead");
            }
        }
    }

    /// <summary>
    /// Force copies the bundled Schedule database to the target path, overwriting any existing file.
    /// Used when migration fails to recover with a clean database from the app bundle.
    /// </summary>
    private async Task CopyScheduleDatabaseFromResourceForceAsync(string dbPath)
    {
        var scheduleDbResourceFile = AppConstants.Database.ScheduleDatabaseFileName;
        var dbDirectory = System.IO.Path.GetDirectoryName(dbPath);

        if (!string.IsNullOrEmpty(dbDirectory) && !System.IO.Directory.Exists(dbDirectory))
        {
            System.IO.Directory.CreateDirectory(dbDirectory);
        }

        await storageService.CopyResourceFile(
            scheduleDbResourceFile,
            dbDirectory ?? storageService.StorageRoot,
            System.IO.Path.GetFileName(dbPath));

        Log.Logger.Information("[BOOTSTRAP] Force copied Schedule database from bundled resource (recovery)");
    }

    /// <summary>
    /// Best-effort delete for SQLite WAL/SHM sidecar files (may be locked briefly after main DB delete).
    /// </summary>
    private static void TryDeleteAuxiliaryDbFile(string path)
    {
        try
        {
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
        catch (System.IO.IOException ex)
        {
            Log.Logger.Debug(ex, "[BOOTSTRAP] Could not delete auxiliary file {Path}", path);
        }
        catch (System.UnauthorizedAccessException ex)
        {
            Log.Logger.Debug(ex, "[BOOTSTRAP] Could not delete auxiliary file {Path}", path);
        }
    }

    /// <summary>
    /// Deletes old database file and its auxiliary files (WAL, SHM) if they exist.
    /// </summary>
    private async Task DeleteOldDatabaseFilesAsync(string dbPath, string description)
    {
        if (!System.IO.File.Exists(dbPath))
        {
            return;
        }

        try
        {
            System.IO.File.Delete(dbPath);
            var oldWalPath = dbPath + "-wal";
            var oldShmPath = dbPath + "-shm";
            TryDeleteAuxiliaryDbFile(oldWalPath);
            TryDeleteAuxiliaryDbFile(oldShmPath);
            Log.Logger.Information("[BOOTSTRAP] Deleted {Description}: {DbPath}", description, dbPath);
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex, "[BOOTSTRAP] Failed to delete {Description} (non-critical): {DbPath}", description, dbPath);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Deletes a legacy file if it exists.
    /// </summary>
    private async Task DeleteLegacyFileAsync(string filePath, string description)
    {
        if (!System.IO.File.Exists(filePath))
        {
            return;
        }

        try
        {
            System.IO.File.Delete(filePath);
            Log.Logger.Information("[BOOTSTRAP] Deleted legacy {Description}: {FilePath}", description, filePath);
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex, "[BOOTSTRAP] Failed to delete legacy {Description} (non-critical): {FilePath}", description, filePath);
        }

        await Task.CompletedTask;
    }
}

