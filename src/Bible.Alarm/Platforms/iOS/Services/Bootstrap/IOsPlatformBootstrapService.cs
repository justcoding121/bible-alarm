#nullable enable

using Bible.Alarm.Services.Bootstrap.Interfaces;

namespace Bible.Alarm.Platforms.iOS.Services.Bootstrap;

/// <summary>
/// iOS platform initialization service.
/// iOS platform-specific setup (background tasks, notifications) is handled
/// directly in AppDelegate, so this is a no-op implementation.
/// </summary>
public class IOsPlatformBootstrapService : IPlatformBootstrapService
{
    public Task InitializeAsync()
    {
        // iOS-specific initialization is handled in AppDelegate.FinishedLaunching()
        // - Background tasks: SetupBackgroundTasks()
        // - Notifications: SetupNotifications()
        // No additional initialization needed here.
        return Task.CompletedTask;
    }
}
