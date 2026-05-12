#nullable enable

using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class CreateScheduleSuccessActionTests
{
    [Fact]
    public void Constructor_sets_Schedule()
    {
        var row = new ScheduleStateItem { Id = 100 };

        var sut = new CreateScheduleSuccessAction(row);

        Assert.Same(row, sut.Schedule);
    }
}
