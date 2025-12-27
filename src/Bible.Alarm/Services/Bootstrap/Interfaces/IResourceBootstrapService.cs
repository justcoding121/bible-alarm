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
}

