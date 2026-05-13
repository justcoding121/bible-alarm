#nullable enable

using Bible.Alarm.Stores.Actions.Schedule;

namespace Bible.Alarm.Tests;

public sealed class DeleteScheduleActionRecordEqualityTests
{
    [Fact]
    public void Instances_with_same_schedule_id_are_equal()
    {
        var a = new DeleteScheduleAction(42);
        var b = new DeleteScheduleAction(a.ScheduleId);
        Assert.Equal(a, b);
    }
}
