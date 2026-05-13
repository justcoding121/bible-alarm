#nullable enable

using Bible.Alarm.Stores;

namespace Bible.Alarm.Tests;

public sealed class PendingScheduleLoadRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_field_values_are_equal()
    {
        var a = new PendingScheduleLoad(ScheduleId: 1, IsEnabled: false);
        var b = new PendingScheduleLoad(a.ScheduleId, a.IsEnabled);
        Assert.Equal(a, b);
    }
}
