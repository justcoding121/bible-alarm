#nullable enable

using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Stores.Reducers;

namespace Bible.Alarm.Tests;

public sealed class ContainerReadinessReducerTests
{
    private static ObservableHashSet<ScheduleStateItem> Schedules(params ScheduleStateItem[] items)
    {
        var set = new ObservableHashSet<ScheduleStateItem>();
        foreach (var item in items)
        {
            set.Add(item);
        }

        return set;
    }

    private static ScheduleStateItem MinimalSchedule() =>
        new()
        {
            Id = 1,
            Name = "S",
            IsEnabled = true,
            Hour = 6,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = false,
        };

    [Fact]
    public void OnContainerReady_sets_named_flag_and_returns_new_state()
    {
        var prior = new ApplicationState(Schedules(MinimalSchedule()));

        var next = ContainerReadinessReducer.OnContainerReady(prior, new ContainerReadyAction("MusicSelection"));

        Assert.NotSame(prior, next);
        Assert.True(next.ContainerReadiness.MusicSelection);
        Assert.False(next.ContainerReadiness.AllReady);
    }

    [Fact]
    public void OnContainerReady_ignores_duplicate_dispatch_for_same_container()
    {
        var prior = new ApplicationState(Schedules(MinimalSchedule()));
        var once = ContainerReadinessReducer.OnContainerReady(prior, new ContainerReadyAction("AlarmSettings"));

        var twice = ContainerReadinessReducer.OnContainerReady(once, new ContainerReadyAction("AlarmSettings"));

        Assert.Same(once, twice);
        Assert.True(once.ContainerReadiness.AlarmSettings);
    }

    [Fact]
    public void OnContainerReady_unknown_name_leaves_flags_unchanged_but_allocates_new_state()
    {
        var prior = new ApplicationState(Schedules(MinimalSchedule()));

        var next = ContainerReadinessReducer.OnContainerReady(prior, new ContainerReadyAction("NotARealContainer"));

        Assert.NotSame(prior, next);
        Assert.False(next.ContainerReadiness.MusicSelection);
    }

    [Fact]
    public void OnResetContainerReadiness_clears_all_flags()
    {
        var seeded = ContainerReadinessReducer.OnContainerReady(
            new ApplicationState(Schedules(MinimalSchedule())),
            new ContainerReadyAction("ScheduleDetails"));

        var reset = ContainerReadinessReducer.OnResetContainerReadiness(seeded, new ResetContainerReadinessAction());

        Assert.NotSame(seeded, reset);
        Assert.False(reset.ContainerReadiness.ScheduleDetails);
        Assert.Equal(ContainerReadiness.NotReady, reset.ContainerReadiness);
    }
}
