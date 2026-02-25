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
    /// Runs only on version change (new media index already copied and overwritten). Handles orphaned
    /// schedule/alarm music first, then runs the ad-hoc non-EnglishSpanish fetch for remaining schedules.
    /// Must be called after both database and resource bootstrap complete.
    /// </summary>
    Task MigrateNonEnglishMediaDataAsync();
}

