#nullable enable

namespace Bible.Alarm.Services.Media.Interfaces;

/// <summary>
/// Service for managing media index version tracking.
/// Uses Preferences for storage (primary) with backward compatibility for version.dat file (legacy).
/// Used to determine if the media index database needs to be updated or migrated.
/// </summary>
public interface IMediaIndexVersionService
{
    /// <summary>
    /// Checks if the version exists (in Preferences or version.dat file).
    /// </summary>
    /// <returns>True if version exists, false otherwise.</returns>
    Task<bool> VersionFileExistsAsync();

    /// <summary>
    /// Reads the version string from Preferences (primary) or version.dat file (fallback).
    /// Automatically migrates from version.dat to Preferences if found in legacy file.
    /// </summary>
    /// <returns>The version string, or null if not found.</returns>
    Task<string?> ReadVersionAsync();

    /// <summary>
    /// Checks if the stored version matches the current app version.
    /// </summary>
    /// <returns>True if versions match, false if they differ or version doesn't exist.</returns>
    Task<bool> IsVersionCurrentAsync();

    /// <summary>
    /// Saves the current app version to Preferences (primary) and version.dat (for backward compatibility).
    /// </summary>
    Task SaveCurrentVersionAsync();
}

