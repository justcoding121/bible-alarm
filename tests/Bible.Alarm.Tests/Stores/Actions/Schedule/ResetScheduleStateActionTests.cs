#nullable enable

using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Stores.Reducers;

namespace Bible.Alarm.Tests;

public sealed class ResetScheduleStateActionTests
{
    private static ScheduleStateItem OneSchedule(int id = 1) =>
        new()
        {
            Id = id,
            Name = "N",
            IsEnabled = true,
            Hour = 6,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = false,
        };

    [Fact]
    public void ApplicationOverlayReducer_OnResetScheduleState_clears_current_and_readiness_but_preserves_overlay_visibility()
    {
        var prior = new ApplicationState([OneSchedule(1)], currentSchedule: OneSchedule(2),
            containerReadiness: ContainerReadiness.AllContainersReady)
        {
            IsSchedulePageOverlayVisible = true,
            IsHomePageOverlayVisible = false,
        };

        var next = ApplicationOverlayReducer.OnResetScheduleState(prior, new ResetScheduleStateAction());

        Assert.Null(next.CurrentSchedule);
        Assert.Single(next.Schedules);
        Assert.True(next.IsSchedulePageOverlayVisible);
        Assert.False(next.IsHomePageOverlayVisible);
        Assert.False(next.ContainerReadiness.AllReady);
    }
}
