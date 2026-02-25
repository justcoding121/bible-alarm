#nullable enable

namespace Bible.Alarm.Services.Bootstrap.Interfaces;

/// <summary>
/// Service for copying resource files during bootstrap.
/// </summary>
public interface IResourceBootstrapService
{
    /// <summary>
    /// Copies required resource files to storage (e.g., silent_preparing.mp3 for Android Auto dummy tracks).
    /// </summary>
    Task CopyResourcesAsync();

    /// <summary>
    /// True when the media index was replaced this bootstrap run (version change).
    /// </summary>
    bool WasMediaIndexReplacedThisRun();

    /// <summary>
    /// Migrates non-English media data from old media index to new packaged index.
    /// Only run when <see cref="WasMediaIndexReplacedThisRun"/> is true (version change).
    /// Must be called after both database and resource bootstrap complete.
    /// </summary>
    Task MigrateNonEnglishMediaDataAsync();
}

