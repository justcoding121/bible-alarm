#nullable enable

using Bible.Alarm.Stores.Actions.Schedule;

namespace Bible.Alarm.Tests;

public sealed class ResetContainerReadinessActionRecordEqualityTests
{
    [Fact]
    public void Parameterless_instances_are_equal()
    {
        var a = new ResetContainerReadinessAction();
        var b = new ResetContainerReadinessAction();
        Assert.Equal(a, b);
    }
}
