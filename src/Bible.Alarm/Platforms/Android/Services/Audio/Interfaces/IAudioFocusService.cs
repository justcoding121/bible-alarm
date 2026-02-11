#nullable enable
namespace Bible.Alarm.Platforms.Android.Services.Audio.Interfaces;

/// <summary>
/// Manages audio focus requests and releases for media playback.
/// </summary>
public interface IAudioFocusService
{
    void RequestAudioFocus();
    void ReleaseAudioFocus();
}
