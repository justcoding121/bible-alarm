#nullable enable

using Bible.Alarm.Stores.Effects;

namespace Bible.Alarm.Tests;

public sealed class ScheduleEffectsOptionalDepsRecordEqualityTests
{
    [Fact]
    public void Default_instances_compare_equal()
    {
        var a = new ScheduleEffectsOptionalDeps();
        var b = new ScheduleEffectsOptionalDeps();
        Assert.Equal(a, b);
    }
}
