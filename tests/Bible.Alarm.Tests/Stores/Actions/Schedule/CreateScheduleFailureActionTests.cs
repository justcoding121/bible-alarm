#nullable enable

using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class CreateScheduleFailureActionTests
{
    [Fact]
    public void Constructor_sets_Schedule_and_Error()
    {
        var row = new ScheduleStateItem { Id = 5 };

        var sut = new CreateScheduleFailureAction(row, "db failed");

        Assert.Same(row, sut.Schedule);
        Assert.Equal("db failed", sut.Error);
    }
}
