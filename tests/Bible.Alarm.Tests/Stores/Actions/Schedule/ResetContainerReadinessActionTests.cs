#nullable enable

using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Stores.Reducers;

namespace Bible.Alarm.Tests;

public sealed class ResetContainerReadinessActionTests
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
    public void ContainerReadinessReducer_OnResetContainerReadiness_clears_all_flags()
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
