#nullable enable

using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Stores.Reducers;

namespace Bible.Alarm.Tests;

public sealed class ApplicationOverlayReducerTests
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
    public void OnSetHomePageOverlay_toggles_visibility_and_preserves_schedule_overlay_flag()
    {
        var prior = new ApplicationState([]) { IsSchedulePageOverlayVisible = true };
        var next = ApplicationOverlayReducer.OnSetHomePageOverlay(prior, new SetHomePageOverlayAction { IsVisible = true });

        Assert.True(next.IsHomePageOverlayVisible);
        Assert.True(next.IsSchedulePageOverlayVisible);
    }

    [Fact]
    public void OnSetSchedulePageOverlay_when_visible_unchanged_returns_same_state_instance()
    {
        var prior = new ApplicationState([]) { IsSchedulePageOverlayVisible = true };
        var action = new SetSchedulePageOverlayAction { IsVisible = true };

        Assert.Same(prior, ApplicationOverlayReducer.OnSetSchedulePageOverlay(prior, action));
    }

    [Fact]
    public void OnSetSchedulePageOverlay_when_showing_resets_ContainerReadiness_and_sets_visible()
    {
        var readiness = ContainerReadiness.AllContainersReady;
        var prior = new ApplicationState([], currentSchedule: OneSchedule(),
            containerReadiness: readiness)
        {
            IsSchedulePageOverlayVisible = false,
        };

        var next = ApplicationOverlayReducer.OnSetSchedulePageOverlay(prior, new SetSchedulePageOverlayAction { IsVisible = true });

        Assert.True(next.IsSchedulePageOverlayVisible);
        Assert.Equal(ContainerReadiness.NotReady.BiblePublicationSelection, next.ContainerReadiness.BiblePublicationSelection);
        Assert.False(next.ContainerReadiness.AllReady);
    }

    [Fact]
    public void OnSetSchedulePageOverlay_when_hiding_preserves_prior_container_readiness()
    {
        var readiness = ContainerReadiness.AllContainersReady;
        var prior = new ApplicationState([], currentSchedule: OneSchedule(),
            containerReadiness: readiness)
        {
            IsSchedulePageOverlayVisible = true,
        };

        var next = ApplicationOverlayReducer.OnSetSchedulePageOverlay(prior, new SetSchedulePageOverlayAction { IsVisible = false });

        Assert.False(next.IsSchedulePageOverlayVisible);
        Assert.Equal(readiness.AllReady, next.ContainerReadiness.AllReady);
    }
}
