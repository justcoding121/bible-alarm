#nullable enable

using Bible.Alarm.Services.Bootstrap.Interfaces;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Services.Bootstrap;

/// <summary>
/// Service for initializing and migrating databases during bootstrap.
/// </summary>
public class DatabaseBootstrapService : IDatabaseBootstrapService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IScheduleDatabaseVersionService scheduleVersionService;
    private readonly IMediaMigrationService mediaMigrationService;
    private readonly IStorageService storageService;

    public DatabaseBootstrapService(
        IServiceScopeFactory scopeFactory,
        IScheduleDatabaseVersionService scheduleVersionService,
        IMediaMigrationService mediaMigrationService,
        IStorageService storageService)
    {
        this.scopeFactory = scopeFactory;
        this.scheduleVersionService = scheduleVersionService;
        this.mediaMigrationService = mediaMigrationService;
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
        // Optimize: Copy from bundled resource on first launch, or check version to skip migration check
#if DEBUG
        var scheduleDbStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
        var scheduleDb = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        // Check if database file exists
        var dbPath = scheduleDb.Database.GetDbConnection().DataSource;
        var dbExists = System.IO.File.Exists(dbPath);

        // If database doesn't exist, try copying from bundled resource first
        // This eliminates the need for migrations on clean install (saves ~1.2 seconds)
        if (!dbExists)
        {
            await CopyScheduleDatabaseFromResourceIfNeededAsync(scope, scheduleDb, dbPath);
            dbExists = System.IO.File.Exists(dbPath); // Re-check after copy attempt
        }

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
            // If GetPendingMigrationsAsync fails (e.g., database is corrupted), try to migrate
            Log.Logger.Warning(ex, "[BOOTSTRAP] Failed to check pending migrations, attempting to migrate database");
            try
            {
                await scheduleDb.Database.MigrateAsync();
                Log.Logger.Information("[BOOTSTRAP] Schedule database migration completed after error recovery");
            }
            catch (Exception migrateEx)
            {
                Log.Logger.Error(migrateEx, "[BOOTSTRAP] Failed to migrate Schedule database, database may be corrupted");
                throw;
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
#endif

        // Migrate Media database if it exists and is from a previous app version
        // Note: App is packaged with latest media index database, so this primarily
        // handles users upgrading from previous app versions
#if DEBUG
        var mediaDbStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
        await mediaMigrationService.MigrateIfNeededAsync();
#if DEBUG
        var mediaDbElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - mediaDbStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        Log.Logger.Information("[BOOTSTRAP] Media database migration completed in {ElapsedMs:F2}ms", mediaDbElapsed);

        var dbInitElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - dbInitStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        Log.Logger.Information("[BOOTSTRAP] Database initialization completed in {ElapsedMs:F2}ms", dbInitElapsed);
#endif
    }

    private async Task CopyScheduleDatabaseFromResourceIfNeededAsync(
        IServiceScope scope,
        ScheduleDbContext scheduleDb,
        string dbPath)
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
}

