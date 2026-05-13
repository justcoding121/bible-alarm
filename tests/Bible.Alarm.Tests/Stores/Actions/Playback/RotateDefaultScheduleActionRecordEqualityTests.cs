#nullable enable

using Bible.Alarm.Stores.Actions.Playback;

namespace Bible.Alarm.Tests;

public sealed class RotateDefaultScheduleActionRecordEqualityTests
{
    [Fact]
    public void Parameterless_instances_are_equal()
    {
        var a = new RotateDefaultScheduleAction();
        var b = new RotateDefaultScheduleAction();
        Assert.Equal(a, b);
    }
}
