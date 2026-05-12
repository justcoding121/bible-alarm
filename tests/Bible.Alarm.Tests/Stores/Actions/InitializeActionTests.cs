#nullable enable

using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class InitializeActionTests
{
    [Fact]
    public void Constructor_null_scheduleList_becomes_empty_hashset()
    {
        var sut = new InitializeAction(null!);

        Assert.NotNull(sut.ScheduleList);
        Assert.Empty(sut.ScheduleList);
    }

    [Fact]
    public void Constructor_preserves_non_null_schedule_list()
    {
        var schedules = new ObservableHashSet<ScheduleStateItem> { new() { Id = 1 } };

        var sut = new InitializeAction(schedules);

        Assert.Same(schedules, sut.ScheduleList);
    }
}
