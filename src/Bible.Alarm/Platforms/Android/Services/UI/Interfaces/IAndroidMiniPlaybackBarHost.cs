#nullable enable

namespace Bible.Alarm.Platforms.Android.Services.UI.Interfaces;

public interface IAndroidMiniPlaybackBarHost
{
    void Attach();
    void Detach();

    /// <summary>
    /// Tells the host whether the PlaybackModal is currently on screen.
    /// When true the bar is forcibly hidden; when false the bar reverts
    /// to whatever <c>MiniPlaybackBarViewModel.IsVisible</c> dictates.
    /// </summary>
    void SetPlaybackModalActive(bool isActive);

    /// <summary>
    /// Re-attaches to the current Activity if the native view was lost
    /// (e.g. after Activity recreation due to memory pressure).
    /// </summary>
    void EnsureAttached();
}
