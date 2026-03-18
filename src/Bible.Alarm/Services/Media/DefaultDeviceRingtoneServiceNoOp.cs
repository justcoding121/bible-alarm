#nullable enable

using Bible.Alarm.Services.Media.Interfaces;

namespace Bible.Alarm.Services.Media;

/// <summary>
/// No-op when no platform ringtone implementation is registered.
/// </summary>
public sealed class DefaultDeviceRingtoneServiceNoOp : IDefaultDeviceRingtoneService
{
    public void StartLoopingAlarmRingtone()
    {
    }

    public void Stop()
    {
    }
}
