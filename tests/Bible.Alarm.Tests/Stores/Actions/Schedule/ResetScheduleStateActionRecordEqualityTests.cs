#nullable enable

using Bible.Alarm.Stores.Actions.Schedule;

namespace Bible.Alarm.Tests;

public sealed class ResetScheduleStateActionRecordEqualityTests
{
    [Fact]
    public void Parameterless_instances_are_equal()
    {
        var a = new ResetScheduleStateAction();
        var b = new ResetScheduleStateAction();
        Assert.Equal(a, b);
    }
}
