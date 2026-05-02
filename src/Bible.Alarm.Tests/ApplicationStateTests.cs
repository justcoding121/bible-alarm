#nullable enable

using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class ApplicationStateTests
{
    [Fact]
    public void Parameterless_constructor_sets_defaults()
    {
        var sut = new ApplicationState();

        Assert.NotNull(sut.Schedules);
        Assert.Empty(sut.Schedules);
        Assert.Null(sut.CurrentSchedule);
        Assert.False(sut.IsHomePageOverlayVisible);
        Assert.False(sut.IsSchedulePageOverlayVisible);
        Assert.Equal(ContainerReadiness.NotReady, sut.ContainerReadiness);
        Assert.Null(sut.PendingScheduleLoad);
    }

    [Fact]
    public void Constructor_accepts_schedules_and_overlay_flags()
    {
        var schedules = new ObservableHashSet<ScheduleStateItem>();
        var current = new ScheduleStateItem { Id = 3, Name = "Test" };
        var readiness = ContainerReadiness.AllContainersReady;
        var pending = new PendingScheduleLoad(9, true);

        var sut = new ApplicationState(schedules, current, true, false, readiness, pending);

        Assert.Same(schedules, sut.Schedules);
        Assert.Same(current, sut.CurrentSchedule);
        Assert.True(sut.IsHomePageOverlayVisible);
        Assert.False(sut.IsSchedulePageOverlayVisible);
        Assert.Equal(readiness, sut.ContainerReadiness);
        Assert.Equal(pending, sut.PendingScheduleLoad);
    }

    [Fact]
    public void Constructor_null_schedules_becomes_empty_hash_set()
    {
        var sut = new ApplicationState(null!);

        Assert.NotNull(sut.Schedules);
        Assert.Empty(sut.Schedules);
    }
}
