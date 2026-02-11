#nullable enable
using Android.Media;

namespace Bible.Alarm.Platforms.Android.Services.Audio.Interfaces;

/// <summary>
/// Listener for audio focus changes. Implemented by the platform to receive focus change callbacks.
/// </summary>
public interface IAudioFocusListener
{
    void OnAudioFocusChange(AudioFocus focusChange);
}
