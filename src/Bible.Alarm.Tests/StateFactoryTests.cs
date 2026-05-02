#nullable enable

using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Stores.Reducers.Services;

namespace Bible.Alarm.Tests;

public sealed class StateFactoryTests
{
    [Fact]
    public void CreateUpdatedState_preserves_collections_and_overlays_but_swaps_current_schedule()
    {
        var schedules = new ObservableHashSet<ScheduleStateItem>();
        var pending = new PendingScheduleLoad(3, true);
        var state = new ApplicationState(
            schedules,
            currentSchedule: new ScheduleStateItem { Id = 1 },
            isHomePageOverlayVisible: true,
            isSchedulePageOverlayVisible: false,
            containerReadiness: ContainerReadiness.AllContainersReady,
            pendingScheduleLoad: pending);
        var updatedSchedule = new ScheduleStateItem { Id = 2 };

        var next = StateFactory.CreateUpdatedState(state, updatedSchedule);

        Assert.Same(schedules, next.Schedules);
        Assert.Same(updatedSchedule, next.CurrentSchedule);
        Assert.True(next.IsHomePageOverlayVisible);
        Assert.False(next.IsSchedulePageOverlayVisible);
        Assert.Equal(ContainerReadiness.AllContainersReady, next.ContainerReadiness);
        Assert.Equal(pending, next.PendingScheduleLoad);
    }

    [Fact]
    public void CreateStateWithSchedules_replaces_schedule_set_and_keeps_current_when_unspecified()
    {
        var oldSet = new ObservableHashSet<ScheduleStateItem>();
        var current = new ScheduleStateItem { Id = 9 };
        var state = new ApplicationState(oldSet, current);
        var newSet = new ObservableHashSet<ScheduleStateItem>();

        var next = StateFactory.CreateStateWithSchedules(state, newSet);

        Assert.Same(newSet, next.Schedules);
        Assert.Same(current, next.CurrentSchedule);
    }

    [Fact]
    public void CreateStateWithSchedules_can_override_current_schedule()
    {
        var state = new ApplicationState([]);
        var newSet = new ObservableHashSet<ScheduleStateItem>();
        var replacement = new ScheduleStateItem { Id = 42 };

        var next = StateFactory.CreateStateWithSchedules(state, newSet, replacement);

        Assert.Same(newSet, next.Schedules);
        Assert.Same(replacement, next.CurrentSchedule);
    }
}
