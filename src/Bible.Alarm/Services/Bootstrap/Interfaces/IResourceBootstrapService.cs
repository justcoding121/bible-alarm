#nullable enable

namespace Bible.Alarm.Services.Bootstrap.Interfaces;

/// <summary>
/// Service for copying resource files during bootstrap.
/// </summary>
public interface IResourceBootstrapService
{
    /// <summary>
    /// Copies required resource files to storage (e.g., silent.mp3 for Android Auto).
    /// </summary>
    Task CopyResourcesAsync();

    /// <summary>
    /// Migrates non-English media data from old media index to new packaged index.
    /// Must be called after both database and resource bootstrap complete.
    /// </summary>
    Task MigrateNonEnglishMediaDataAsync();
}

