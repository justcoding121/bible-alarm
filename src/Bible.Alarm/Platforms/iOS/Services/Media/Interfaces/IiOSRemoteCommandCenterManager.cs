#nullable enable
namespace Bible.Alarm.Platforms.iOS.Services.Media.Interfaces;

/// <summary>
/// Manages MPRemoteCommandCenter for iOS system media controls.
/// </summary>
public interface IiOSRemoteCommandCenterManager : IDisposable
{
    void RegisterCommands();
    void UnregisterCommands();
    void UpdateCommandAvailability(bool canPlayNext, bool canPlayPrevious, bool isPlaying);
}
