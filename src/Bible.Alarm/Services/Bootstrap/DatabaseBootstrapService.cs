#nullable enable

using System.Linq;
using Bible.Alarm.Services.Bootstrap.Interfaces;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
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

        await using var scope = scopeFactory.CreateAsyncScope();

#if DEBUG
        var scheduleDbStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
        var scheduleDb = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
        var dbPath = scheduleDb.Database.GetDbConnection().DataSource;
        var dbDirectory = System.IO.Path.GetDirectoryName(dbPath) ?? "";

        await DeleteLegacyScheduleDatabaseFilesAsync(dbDirectory);

        var dbExists = await EnsureScheduleDatabaseFileExistsAsync(dbPath);
        var versionMatches = dbExists && await scheduleVersionService.IsVersionCurrentAsync();

        LogScheduleDatabaseVersionBranch(versionMatches, dbExists);

        scheduleDb = await MigrateScheduleDatabaseOrRecoverAsync(scheduleDb, dbPath, versionMatches, scope.ServiceProvider);

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

    /// <summary>
    /// Deletes only legacy-named schedule DB files (never the current schedule.db path).
    /// version.dat files are not deleted — MediaIndexVersionService uses them when Preferences are unavailable.
    /// </summary>
    private async Task DeleteLegacyScheduleDatabaseFilesAsync(string dbDirectory)
    {
        var oldDbPath1 = System.IO.Path.Combine(dbDirectory, AppConstants.Database.ScheduleDatabaseLegacyBibleAlarmFileName);
        await DeleteOldDatabaseFilesAsync(oldDbPath1, $"old Schedule database ({AppConstants.Database.ScheduleDatabaseLegacyBibleAlarmFileName})");

        var oldDbPath2 = System.IO.Path.Combine(dbDirectory, AppConstants.Database.ScheduleDatabaseLegacyBibleAlarm2FileName);
        await DeleteOldDatabaseFilesAsync(oldDbPath2, $"old Schedule database ({AppConstants.Database.ScheduleDatabaseLegacyBibleAlarm2FileName})");
    }

    private async Task<bool> EnsureScheduleDatabaseFileExistsAsync(string dbPath)
    {
        var dbExists = System.IO.File.Exists(dbPath);
        if (!dbExists)
        {
            await CopyScheduleDatabaseFromResourceIfNeededAsync(dbPath);
            dbExists = System.IO.File.Exists(dbPath);
        }

        return dbExists;
    }

    private static void LogScheduleDatabaseVersionBranch(bool versionMatches, bool dbExists)
    {
        if (versionMatches)
        {
            Log.Logger.Debug("[BOOTSTRAP] Schedule database version matches current app version, verifying schema...");
            return;
        }

        if (!dbExists)
        {
            Log.Logger.Debug("[BOOTSTRAP] Schedule database file does not exist, will be created from bundled resource or migrations");
        }
        else
        {
            Log.Logger.Debug("[BOOTSTRAP] Schedule database version mismatch or not set, will verify schema and apply migrations if needed");
        }
    }

    private async Task<ScheduleDbContext> MigrateScheduleDatabaseOrRecoverAsync(
        ScheduleDbContext scheduleDb,
        string dbPath,
        bool versionMatches,
        IServiceProvider scopedServices)
    {
        try
        {
            await ApplyPendingScheduleMigrationsAsync(scheduleDb, versionMatches);
            return scheduleDb;
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex, "[BOOTSTRAP] Failed to check/apply migrations, attempting recovery with bundled database");
            return await RecoverScheduleDatabaseAfterMigrationFailureAsync(scheduleDb, dbPath, scopedServices);
        }
    }

    private static async Task ApplyPendingScheduleMigrationsAsync(ScheduleDbContext scheduleDb, bool versionMatches)
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
        else if (versionMatches)
        {
            Log.Logger.Debug("[BOOTSTRAP] Schedule database schema verified - version matches and all migrations applied");
        }
        else
        {
            Log.Logger.Debug("[BOOTSTRAP] Schedule database is already up to date (bundled database had schema), skipping migration");
        }
    }

    private async Task<ScheduleDbContext> RecoverScheduleDatabaseAfterMigrationFailureAsync(
        ScheduleDbContext scheduleDb,
        string dbPath,
        IServiceProvider scopedServices)
    {
        try
        {
            await scheduleDb.Database.CloseConnectionAsync();
            await scheduleDb.DisposeAsync();

            SqliteConnection.ClearAllPools();

            if (System.IO.File.Exists(dbPath))
            {
                const int maxRetries = 5;
                const int retryDelayMs = 100;
                var deleted = false;

                for (var attempt = 0; attempt < maxRetries && !deleted; attempt++)
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
                        throw new IOException(
                            $"[BOOTSTRAP] Could not delete corrupted database file after {maxRetries} attempts, may be locked.",
                            deleteEx);
                    }
                }
            }

            var walPath = dbPath + AppConstants.Database.SqliteWalFileSuffix;
            var shmPath = dbPath + AppConstants.Database.SqliteShmFileSuffix;
            TryDeleteAuxiliaryDbFile(walPath);
            TryDeleteAuxiliaryDbFile(shmPath);

            await CopyScheduleDatabaseFromResourceForceAsync(dbPath);

            if (!System.IO.File.Exists(dbPath))
            {
                throw new InvalidOperationException("Failed to copy bundled database during recovery");
            }

            var freshDb = scopedServices.GetRequiredService<ScheduleDbContext>();

            var pendingAfterRecovery = await freshDb.Database.GetPendingMigrationsAsync();
            if (pendingAfterRecovery.Any())
            {
                Log.Logger.Information(
                    "[BOOTSTRAP] Schedule database recovery: {Count} pending migrations after copying bundled database, applying...",
                    pendingAfterRecovery.Count());
                await freshDb.Database.MigrateAsync();
                Log.Logger.Information("[BOOTSTRAP] Schedule database migrations applied successfully after recovery");
            }
            else
            {
                Log.Logger.Information("[BOOTSTRAP] Schedule database recovery successful - bundled database had correct schema");
            }

            return freshDb;
        }
        catch (Exception recoveryEx)
        {
            throw new InvalidOperationException(
                "Schedule database migration failed and recovery attempt failed. The app cannot continue without a valid database.",
                recoveryEx);
        }
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
    private static async Task DeleteOldDatabaseFilesAsync(string dbPath, string description)
    {
        if (!System.IO.File.Exists(dbPath))
        {
            return;
        }

        try
        {
            System.IO.File.Delete(dbPath);
            var oldWalPath = dbPath + AppConstants.Database.SqliteWalFileSuffix;
            var oldShmPath = dbPath + AppConstants.Database.SqliteShmFileSuffix;
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
}

