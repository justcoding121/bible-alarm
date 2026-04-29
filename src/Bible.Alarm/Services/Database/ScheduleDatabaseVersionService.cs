#nullable enable

using Bible.Alarm.Common.Interfaces.Platform;
using Bible.Alarm.Common.Interfaces.Storage;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.Services.Database;

/// <summary>
/// Service for managing Schedule database version tracking.
/// Uses Preferences for storage to track the app version that last verified/migrated the Schedule database.
/// Used to optimize migration checks by skipping them when the app version hasn't changed.
/// Uses IThreadSafePreferencesService for centralized, thread-safe Preferences access.
/// </summary>
public sealed class ScheduleDatabaseVersionService(
    ILogger logger,
    IVersionFinder versionFinder,
    IThreadSafePreferencesService preferencesService)
    : IScheduleDatabaseVersionService
{
    private const string VersionPreferenceKey = "ScheduleDatabaseVersion";

    public Task<bool> IsVersionCurrentAsync()
    {
        try
        {
            if (!preferencesService.ContainsKey(VersionPreferenceKey))
            {
                return Task.FromResult(false);
            }

            var storedVersion = preferencesService.Get(VersionPreferenceKey, "");
            if (string.IsNullOrEmpty(storedVersion))
            {
                return Task.FromResult(false);
            }

            var currentVersion = versionFinder.GetVersionName();
            var isCurrent = storedVersion == currentVersion;

            if (!isCurrent)
            {
                logger.Debug(
                    AppConstants.Logging.ScheduleDatabaseVersionServiceDiagnosticsLog.VersionMismatchStoredVersusCurrent,
                    storedVersion, currentVersion);
            }

            return Task.FromResult(isCurrent);
        }
        catch (Exception ex)
        {
            // If version check fails (e.g., versionFinder throws), assume version mismatch
            // This ensures we always check migrations when there's uncertainty
            logger.Warning(ex,
                AppConstants.Logging.ScheduleDatabaseVersionServiceDiagnosticsLog.FailedToCheckScheduleDatabaseVersionPerformingMigrationCheck);
            return Task.FromResult(false);
        }
    }

    public Task SaveCurrentVersionAsync()
    {
        try
        {
            var currentVersion = versionFinder.GetVersionName();
            preferencesService.Set(VersionPreferenceKey, currentVersion);
            logger.Debug("Saved Schedule database version {Version} to Preferences", currentVersion);
        }
        catch (Exception ex)
        {
            logger.Error(ex,
                AppConstants.Logging.ScheduleDatabaseVersionServiceDiagnosticsLog.FailedToSaveScheduleDatabaseVersionToPreferences);
            throw;
        }

        return Task.CompletedTask;
    }
}

