using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Services.Database;

/// <summary>
/// Service for managing Media database migrations.
/// Since the app is packaged with the latest media index database, this service primarily
/// handles migrating old databases from previous app versions to match the current app schema.
/// </summary>
public sealed class MediaMigrationService(
    ILogger logger,
    IServiceScopeFactory scopeFactory,
    IMediaIndexVersionService versionService)
    : IMediaMigrationService
{

    public async Task MigrateIfNeededAsync()
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var mediaDb = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            // Check if database file exists
            var dbPath = mediaDb.Database.GetDbConnection().DataSource;
            if (!File.Exists(dbPath))
            {
                // Database doesn't exist yet - will be created from bundled resource by MediaIndexService
                // The bundled database already has the latest schema matching this app version
                return;
            }

            // Optimize: Check if database was just copied from resources (version matches current version)
            // This happens in two scenarios:
            // 1. Clean install: MediaIndexService copies bundled DB and saves version
            // 2. Version mismatch: MediaIndexService copies bundled DB (replacing old one) and saves version
            // In both cases, the bundled database already has the latest schema with all migrations applied,
            // so we can skip migration check entirely
            if (await versionService.IsVersionCurrentAsync())
            {
                // Database was just copied from resources with current version - already has latest schema
                logger.Debug("Media database version matches current app version (was copied from bundled resource), skipping migration check");
                return;
            }

            // Check if we can connect to the database
            var canConnect = await mediaDb.Database.CanConnectAsync();
            if (!canConnect)
            {
                logger.Warning("Cannot connect to Media database - may be corrupted. MediaIndexService will handle re-download.");
                return;
            }

            // Get pending migrations (database is older than app)
            // This handles users upgrading from previous app versions
            var pendingMigrations = await mediaDb.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                logger.Information(
                    "Media database from previous app version has {Count} pending migrations, applying...",
                    pendingMigrations.Count());
                await mediaDb.Database.MigrateAsync();
                logger.Information("Media database migration completed successfully");
            }
            else
            {
                logger.Debug("Media database is already up to date, skipping migration");
            }

            // Database is up to date - no migration needed
            // Note: Since app is packaged with latest database, we shouldn't encounter
            // databases newer than the app version in normal operation
        }
        catch (Exception ex)
        {
            // If migration fails (e.g., database is corrupted),
            // log but don't crash - MediaIndexService will handle re-downloading if needed
            logger.Warning(ex,
                "Could not migrate Media database. " +
                "MediaIndexService will handle re-downloading if needed.");
        }
    }

}

