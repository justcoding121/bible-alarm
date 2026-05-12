#nullable enable

using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class UpdateDraftScheduleActionTests
{
    [Fact]
    public void Constructor_sets_Schedule()
    {
        var row = new ScheduleStateItem { Id = 8 };

        var sut = new UpdateDraftScheduleAction(row);

        Assert.Same(row, sut.Schedule);
    }
}
