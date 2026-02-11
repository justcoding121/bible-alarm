#nullable enable
using Bible.Alarm.Common.Interfaces.UI;

namespace Bible.Alarm.Platforms.Windows.Services.UI.Interfaces;

/// <summary>
/// Windows notification service including media toast (playback) notifications.
/// </summary>
public interface IWindowsNotificationService : INotificationService
{
    void DismissMediaToast();
    void ShowMediaToast(string? title, string? subtitle, string? body, string? artworkUrl, bool canPlayNext = false, bool canPlayPrevious = false, bool isPlaying = false);
}
