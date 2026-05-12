#nullable enable

using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class UpdateScheduleSuccessActionTests
{
    [Fact]
    public void Constructor_defaults_SkipCacheRefresh_to_false()
    {
        var row = new ScheduleStateItem { Id = 4 };

        var sut = new UpdateScheduleSuccessAction(row);

        Assert.Same(row, sut.Schedule);
        Assert.False(sut.SkipCacheRefresh);
    }

    [Fact]
    public void Constructor_accepts_SkipCacheRefresh()
    {
        var row = new ScheduleStateItem { Id = 5 };

        var sut = new UpdateScheduleSuccessAction(row, skipCacheRefresh: true);

        Assert.True(sut.SkipCacheRefresh);
    }
}
