#nullable enable

using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class UpdateScheduleFailureActionTests
{
    [Fact]
    public void Constructor_sets_Schedule_and_Error()
    {
        var row = new ScheduleStateItem { Id = 6 };

        var sut = new UpdateScheduleFailureAction(row, "save failed");

        Assert.Same(row, sut.Schedule);
        Assert.Equal("save failed", sut.Error);
    }
}
