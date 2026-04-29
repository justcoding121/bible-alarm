#nullable enable

using Bible.Alarm.Common.Interfaces.Platform;
using Bible.Alarm.Common.Interfaces.Storage;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.Services.Media;

/// <summary>
/// Service for managing media index version tracking.
/// Uses Preferences for storage (new) with backward compatibility for version.dat file (legacy).
/// Used to determine if the media index database needs to be updated or migrated.
/// Uses IThreadSafePreferencesService for centralized, thread-safe Preferences access.
/// </summary>
public sealed class MediaIndexVersionService(
    ILogger logger,
    IStorageService storageService,
    IVersionFinder versionFinder,
    IThreadSafePreferencesService preferencesService)
    : IMediaIndexVersionService
{
    private readonly Lazy<string> indexRoot = new(() => storageService.StorageRoot);

    private string IndexRoot => indexRoot.Value;
    private string VersionFilePath => Path.Combine(IndexRoot, AppConstants.FilePaths.MediaIndexVersionLegacyFileName);

    public async Task<bool> VersionFileExistsAsync()
    {
        // Check Preferences first (new method)
        if (preferencesService.ContainsKey(AppConstants.GeneralSettingsKeys.MediaIndexVersion))
        {
            return true;
        }

        // Fall back to version.dat for backward compatibility
        return await storageService.FileExists(VersionFilePath);
    }

    public async Task<string?> ReadVersionAsync()
    {
        // Try Preferences first (new method)
        if (preferencesService.ContainsKey(AppConstants.GeneralSettingsKeys.MediaIndexVersion))
        {
            var version = await preferencesService.GetAsync(AppConstants.GeneralSettingsKeys.MediaIndexVersion, "");
            if (!string.IsNullOrEmpty(version))
            {
                return version;
            }
        }

        // Fall back to version.dat for backward compatibility
        if (await storageService.FileExists(VersionFilePath))
        {
            try
            {
                var version = await storageService.ReadFile(VersionFilePath);
                if (!string.IsNullOrEmpty(version))
                {
                    // Migrate from version.dat to Preferences for future use
                    await MigrateVersionToPreferencesAsync(version);
                    return version;
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Failed to read version from {VersionFilePath}", VersionFilePath);
            }
        }

        return null;
    }

    public async Task<bool> IsVersionCurrentAsync()
    {
        var storedVersion = await ReadVersionAsync();
        if (storedVersion == null)
        {
            return false;
        }

        var currentVersion = versionFinder.GetVersionName();
        return storedVersion == currentVersion;
    }

    public async Task SaveCurrentVersionAsync()
    {
        try
        {
            var currentVersion = versionFinder.GetVersionName();

            // Save to Preferences (new method)
            await preferencesService.SetAsync(AppConstants.GeneralSettingsKeys.MediaIndexVersion, currentVersion);
            logger.Debug("Saved current version {Version} to Preferences", currentVersion);

            // Also save to version.dat for backward compatibility during transition
            // This ensures older app versions or code paths that still check version.dat will work
            try
            {
                await storageService.SaveFile(IndexRoot, AppConstants.FilePaths.MediaIndexVersionLegacyFileName, currentVersion);
                logger.Debug("Saved current version {Version} to {VersionFilePath} for backward compatibility", currentVersion, VersionFilePath);
            }
            catch (Exception ex)
            {
                // Log but don't fail - Preferences is the primary storage now
                logger.Warning(ex, "Failed to save version to {VersionFilePath} (non-critical, Preferences is primary)", VersionFilePath);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to save version to Preferences");
            throw;
        }
    }

    /// <summary>
    /// Migrates version from version.dat to Preferences.
    /// Called when version is found in version.dat but not in Preferences.
    /// </summary>
    private async Task MigrateVersionToPreferencesAsync(string version)
    {
        try
        {
            if (!preferencesService.ContainsKey(AppConstants.GeneralSettingsKeys.MediaIndexVersion))
            {
                await preferencesService.SetAsync(AppConstants.GeneralSettingsKeys.MediaIndexVersion, version);
                logger.Debug("Migrated version {Version} from version.dat to Preferences", version);
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail - migration is non-critical
            logger.Warning(ex, "Failed to migrate version to Preferences (non-critical)");
        }
    }
}

