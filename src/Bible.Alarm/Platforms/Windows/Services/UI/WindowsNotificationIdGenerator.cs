#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Platforms.Windows.Services.UI;

internal static class WindowsNotificationIdGenerator
{
    internal static string BuildUniqueId(int scheduleId, DateTimeOffset fireDate) =>
        WindowsToastNotificationUniqueIdComposer.Compose(scheduleId, fireDate);
}

