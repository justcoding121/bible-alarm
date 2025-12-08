namespace Bible.Alarm.Services.Database.Interfaces;

/// <summary>
/// Service for managing Media database migrations.
/// Handles safe migration of Media database, including detection of schema version mismatches.
/// </summary>
public interface IMediaMigrationService : IDisposable
{
    /// <summary>
    /// Migrates the Media database if it's older than the app version.
    /// Detects and warns if database is newer than app (schema version mismatch).
    /// </summary>
    Task MigrateIfNeededAsync();
}

