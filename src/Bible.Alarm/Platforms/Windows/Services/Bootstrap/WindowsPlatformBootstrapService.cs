#nullable enable

using Bible.Alarm.Services.Bootstrap.Interfaces;

namespace Bible.Alarm.Platforms.Windows.Services.Bootstrap;

/// <summary>
/// Windows platform initialization service.
/// Windows doesn't require notification channels or special job scheduling,
/// so this is a no-op implementation.
/// </summary>
public class WindowsPlatformBootstrapService : IPlatformBootstrapService
{
    public Task InitializeAsync()
    {
        // Windows doesn't require platform-specific initialization after bootstrap.
        // Background tasks are handled via Windows Task Scheduler or app lifecycle.
        return Task.CompletedTask;
    }
}
