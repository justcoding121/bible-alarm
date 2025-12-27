#nullable enable

namespace Bible.Alarm.Services.Database.Interfaces;

/// <summary>
/// Service for managing Schedule database version tracking.
/// Uses Preferences for storage to track the app version that last verified/migrated the Schedule database.
/// Used to optimize migration checks by skipping them when the app version hasn't changed.
/// </summary>
public interface IScheduleDatabaseVersionService
{
    /// <summary>
    /// Checks if the stored version matches the current app version.
    /// </summary>
    /// <returns>True if versions match, false if they differ or version doesn't exist.</returns>
    Task<bool> IsVersionCurrentAsync();

    /// <summary>
    /// Saves the current app version to Preferences.
    /// Should be called after successfully verifying/migrating the Schedule database.
    /// </summary>
    Task SaveCurrentVersionAsync();
}

