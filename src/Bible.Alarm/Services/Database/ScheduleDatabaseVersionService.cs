#nullable enable

using Bible.Alarm.Common.Interfaces.Platform;
using Bible.Alarm.Services.Database.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Database;

/// <summary>
/// Service for managing Schedule database version tracking.
/// Uses Preferences for storage to track the app version that last verified/migrated the Schedule database.
/// Used to optimize migration checks by skipping them when the app version hasn't changed.
/// </summary>
public sealed class ScheduleDatabaseVersionService(
    ILogger logger,
    IVersionFinder versionFinder)
    : IScheduleDatabaseVersionService
{
    private const string VersionPreferenceKey = "ScheduleDatabaseVersion";

    public Task<bool> IsVersionCurrentAsync()
    {
        try
        {
            if (!Preferences.ContainsKey(VersionPreferenceKey))
            {
                return Task.FromResult(false);
            }

            var storedVersion = Preferences.Get(VersionPreferenceKey, (string?)null);
            if (string.IsNullOrEmpty(storedVersion))
            {
                return Task.FromResult(false);
            }

            var currentVersion = versionFinder.GetVersionName();
            var isCurrent = storedVersion == currentVersion;

            if (!isCurrent)
            {
                logger.Debug(
                    "Schedule database version mismatch - stored: {StoredVersion}, current: {CurrentVersion}. Migration check needed.",
                    storedVersion, currentVersion);
            }

            return Task.FromResult(isCurrent);
        }
        catch (Exception ex)
        {
            // If version check fails (e.g., versionFinder throws), assume version mismatch
            // This ensures we always check migrations when there's uncertainty
            logger.Warning(ex, "Failed to check Schedule database version, will perform migration check");
            return Task.FromResult(false);
        }
    }

    public Task SaveCurrentVersionAsync()
    {
        try
        {
            var currentVersion = versionFinder.GetVersionName();
            Preferences.Set(VersionPreferenceKey, currentVersion);
            logger.Debug("Saved Schedule database version {Version} to Preferences", currentVersion);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to save Schedule database version to Preferences");
            throw;
        }

        return Task.CompletedTask;
    }
}

