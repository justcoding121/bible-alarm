#nullable enable

using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class DeleteScheduleFailureActionTests
{
    [Fact]
    public void Constructor_sets_id_and_error_and_optional_snapshot()
    {
        var snap = new ScheduleStateItem { Id = 3 };

        var sut = new DeleteScheduleFailureAction(3, "cannot delete", snap);

        Assert.Equal(3, sut.ScheduleId);
        Assert.Equal("cannot delete", sut.Error);
        Assert.Same(snap, sut.Schedule);
    }

    [Fact]
    public void Constructor_accepts_null_schedule_snapshot()
    {
        var sut = new DeleteScheduleFailureAction(9, "timeout");

        Assert.Null(sut.Schedule);
    }
}
