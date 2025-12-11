using Bible.Alarm.Shared.Database;
using Bible.Alarm.Services.Database.Interfaces;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Services.Database;

/// <summary>
/// Service for managing Media database migrations.
/// Since the app is packaged with the latest media index database, this service primarily
/// handles migrating old databases from previous app versions to match the current app schema.
/// </summary>
public class MediaMigrationService(
    ILogger logger,
    IServiceScopeFactory scopeFactory)
    : IMediaMigrationService, IDisposable
{
    private readonly ILogger _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private bool _isDisposed;

    public async Task MigrateIfNeededAsync()
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var mediaDb = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            
            // Check if database file exists
            var dbPath = mediaDb.Database.GetDbConnection().DataSource;
            if (!System.IO.File.Exists(dbPath))
            {
                // Database doesn't exist yet - will be created from bundled resource by MediaIndexService
                // The bundled database already has the latest schema matching this app version
                return;
            }
            
            // Check if we can connect to the database
            var canConnect = await mediaDb.Database.CanConnectAsync();
            if (!canConnect)
            {
                _logger.Warning("Cannot connect to Media database - may be corrupted. MediaIndexService will handle re-download.");
                return;
            }
            
            // Get pending migrations (database is older than app)
            // This handles users upgrading from previous app versions
            var pendingMigrations = await mediaDb.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                _logger.Information(
                    "Media database from previous app version has {Count} pending migrations, applying...", 
                    pendingMigrations.Count());
                await mediaDb.Database.MigrateAsync();
                _logger.Information("Media database migration completed successfully");
                return;
            }
            
            // Database is up to date - no migration needed
            // Note: Since app is packaged with latest database, we shouldn't encounter
            // databases newer than the app version in normal operation
        }
        catch (Exception ex)
        {
            // If migration fails (e.g., database is corrupted),
            // log but don't crash - MediaIndexService will handle re-downloading if needed
            _logger.Warning(ex, 
                "Could not migrate Media database. " +
                "MediaIndexService will handle re-downloading if needed.");
        }
    }
    
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        
        _isDisposed = true;
        
        // IServiceScopeFactory is a singleton, so don't dispose it
        // No event handlers to unsubscribe
    }
}

