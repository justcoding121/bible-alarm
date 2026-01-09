#nullable enable

namespace Bible.Alarm.Services.Bootstrap.Interfaces;

/// <summary>
/// Service for platform-specific initialization that should run after core bootstrap completes.
/// This ensures notification channels, background jobs, and other platform-specific setup
/// occurs regardless of which bootstrap entry point was used.
/// </summary>
public interface IPlatformBootstrapService
{
    /// <summary>
    /// Performs platform-specific initialization.
    /// Called after database, Fluxor, and schedule bootstrap tasks complete.
    /// Implementations should be idempotent (safe to call multiple times).
    /// </summary>
    Task InitializeAsync();
}
