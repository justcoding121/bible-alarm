#nullable enable

using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class ViewScheduleActionTests
{
    [Fact]
    public void Constructor_sets_SelectedSchedule()
    {
        var row = new ScheduleStateItem { Id = 11 };

        var sut = new ViewScheduleAction(row);

        Assert.Same(row, sut.SelectedSchedule);
    }

    [Fact]
    public void Constructor_accepts_null_schedule()
    {
        var sut = new ViewScheduleAction(null);

        Assert.Null(sut.SelectedSchedule);
    }
}
