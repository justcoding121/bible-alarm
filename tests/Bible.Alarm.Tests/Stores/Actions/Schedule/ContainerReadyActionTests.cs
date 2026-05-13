#nullable enable

using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Stores.Reducers;

namespace Bible.Alarm.Tests;

public sealed class ContainerReadyActionTests
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
    public void ContainerReadinessReducer_OnContainerReady_sets_named_flag_and_returns_new_state()
    {
        var prior = new ApplicationState(Schedules(MinimalSchedule()));

        var next = ContainerReadinessReducer.OnContainerReady(prior, new ContainerReadyAction("MusicSelection"));

        Assert.NotSame(prior, next);
        Assert.True(next.ContainerReadiness.MusicSelection);
        Assert.False(next.ContainerReadiness.AllReady);
    }

    [Fact]
    public void ContainerReadinessReducer_OnContainerReady_ignores_duplicate_dispatch_for_same_container()
    {
        var prior = new ApplicationState(Schedules(MinimalSchedule()));
        var once = ContainerReadinessReducer.OnContainerReady(prior, new ContainerReadyAction("AlarmSettings"));

        var twice = ContainerReadinessReducer.OnContainerReady(once, new ContainerReadyAction("AlarmSettings"));

        Assert.Same(once, twice);
        Assert.True(once.ContainerReadiness.AlarmSettings);
    }

    [Fact]
    public void ContainerReadinessReducer_OnContainerReady_unknown_name_leaves_flags_unchanged_but_allocates_new_state()
    {
        var prior = new ApplicationState(Schedules(MinimalSchedule()));

        var next = ContainerReadinessReducer.OnContainerReady(prior, new ContainerReadyAction("NotARealContainer"));

        Assert.NotSame(prior, next);
        Assert.False(next.ContainerReadiness.MusicSelection);
    }

    [Fact]
    public void ContainerReadinessReducer_OnContainerReady_sequential_readiness_sets_AllReady_when_all_containers_reported()
    {
        var state = new ApplicationState(Schedules(MinimalSchedule()));

        state = ContainerReadinessReducer.OnContainerReady(state, new ContainerReadyAction("BiblePublicationSelection"));
        state = ContainerReadinessReducer.OnContainerReady(state, new ContainerReadyAction("MusicSelection"));
        state = ContainerReadinessReducer.OnContainerReady(state, new ContainerReadyAction("NumberOfTrack"));
        state = ContainerReadinessReducer.OnContainerReady(state, new ContainerReadyAction("ScheduleDetails"));
        state = ContainerReadinessReducer.OnContainerReady(state, new ContainerReadyAction("AlarmSettings"));

        Assert.True(state.ContainerReadiness.AllReady);
        Assert.True(state.ContainerReadiness.BiblePublicationSelection);
        Assert.True(state.ContainerReadiness.NumberOfTrack);
    }

    [Fact]
    public void ContainerReadinessReducer_OnContainerReady_ignores_duplicate_BiblePublicationSelection()
    {
        var prior = new ApplicationState(Schedules(MinimalSchedule()));
        var once = ContainerReadinessReducer.OnContainerReady(prior, new ContainerReadyAction("BiblePublicationSelection"));
        var twice = ContainerReadinessReducer.OnContainerReady(once, new ContainerReadyAction("BiblePublicationSelection"));

        Assert.Same(once, twice);
        Assert.True(once.ContainerReadiness.BiblePublicationSelection);
    }

    [Fact]
    public void ContainerReadinessReducer_OnContainerReady_unknown_name_preserves_existing_flags()
    {
        var withMusic = ContainerReadinessReducer.OnContainerReady(
            new ApplicationState(Schedules(MinimalSchedule())),
            new ContainerReadyAction("MusicSelection"));

        var withUnknown = ContainerReadinessReducer.OnContainerReady(
            withMusic,
            new ContainerReadyAction("NotARealContainer"));

        Assert.NotSame(withMusic, withUnknown);
        Assert.True(withUnknown.ContainerReadiness.MusicSelection);
        Assert.False(withUnknown.ContainerReadiness.BiblePublicationSelection);
    }
}
