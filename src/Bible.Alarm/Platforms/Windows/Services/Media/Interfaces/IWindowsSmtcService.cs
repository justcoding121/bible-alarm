#nullable enable
namespace Bible.Alarm.Platforms.Windows.Services.Media.Interfaces;

/// <summary>
/// Windows-specific service for System Media Transport Controls (SMTC) button events.
/// </summary>
public interface IWindowsSmtcService : IDisposable
{
    Task InitializeAsync();
    void UpdateButtonStates(bool canPlayNext, bool canPlayPrevious);
}
