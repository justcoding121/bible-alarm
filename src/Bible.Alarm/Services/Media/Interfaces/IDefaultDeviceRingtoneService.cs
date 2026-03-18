#nullable enable

namespace Bible.Alarm.Services.Media.Interfaces;

/// <summary>
/// Plays the device default ringtone in a loop until stopped (e.g. alarm playback error + Retry/Dismiss).
/// </summary>
public interface IDefaultDeviceRingtoneService
{
    void StartLoopingAlarmRingtone();

    void Stop();
}
