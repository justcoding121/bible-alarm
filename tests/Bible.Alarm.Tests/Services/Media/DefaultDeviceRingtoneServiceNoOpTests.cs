#nullable enable

using Bible.Alarm.Services.Media;

namespace Bible.Alarm.Tests;

public sealed class DefaultDeviceRingtoneServiceNoOpTests
{
    [Fact]
    public void StartLoopingAlarmRingtone_and_Stop_are_no_op()
    {
        var sut = new DefaultDeviceRingtoneServiceNoOp();
        sut.StartLoopingAlarmRingtone();
        sut.Stop();
    }
}
