#nullable enable

using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class AddScheduleSuccessActionTests
{
    [Fact]
    public void Constructor_sets_Schedule()
    {
        var row = new ScheduleStateItem { Id = 30 };

        var sut = new AddScheduleSuccessAction(row);

        Assert.Same(row, sut.Schedule);
    }
}
