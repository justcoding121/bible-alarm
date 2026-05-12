#nullable enable

using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class SchedulePropertyManagerTests
{
    private sealed class FakeAppState(ApplicationState snapshot) : Fluxor.IState<ApplicationState>
    {
#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067

        public ApplicationState Value => snapshot;
    }

    [Fact]
    public void IsNewSchedule_flips_IsExistingSchedule_exclusive_pair()
    {
        var sut = new SchedulePropertyManager(new FakeAppState(new ApplicationState()), TestLogging.CreateLogger());

        sut.IsNewSchedule = true;

        Assert.True(sut.IsNewSchedule);
        Assert.False(sut.IsExistingSchedule);

        sut.IsNewSchedule = false;

        Assert.False(sut.IsNewSchedule);
        Assert.True(sut.IsExistingSchedule);
    }

    [Fact]
    public void IsSchedulePageOverlayVisible_ignores_redundant_writes_to_same_value()
    {
        var sut = new SchedulePropertyManager(new FakeAppState(new ApplicationState()), TestLogging.CreateLogger());

        sut.IsSchedulePageOverlayVisible = false;
        sut.IsSchedulePageOverlayVisible = false;

        Assert.False(sut.IsSchedulePageOverlayVisible);
    }

    [Fact]
    public void Busy_flags_update_independently()
    {
        var sut = new SchedulePropertyManager(new FakeAppState(new ApplicationState()), TestLogging.CreateLogger());

        sut.IsCancelBusy = true;
        sut.IsSaveBusy = true;
        sut.IsDeleteBusy = true;

        Assert.True(sut.IsCancelBusy);
        Assert.True(sut.IsSaveBusy);
        Assert.True(sut.IsDeleteBusy);
    }

    [Fact]
    public void Computed_properties_reflect_CurrentSchedule_via_helper()
    {
        var schedule = new ScheduleStateItem
        {
            Id = 44,
            Name = "Morning",
            IsEnabled = true,
            Hour = 9,
            Minute = 45,
            Second = 30,
            DaysOfWeek = WeekDays.Monday,
            MusicEnabled = true,
        };

        var app = new ApplicationState([], currentSchedule: schedule);
        var sut = new SchedulePropertyManager(new FakeAppState(app), TestLogging.CreateLogger());

        Assert.Equal("Morning", sut.Name);
        Assert.True(sut.IsEnabled);
        Assert.Equal(schedule.DaysOfWeek, sut.DaysOfWeek);
        Assert.Equal(new TimeSpan(9, 45, 30), sut.Time);
        Assert.True(sut.MusicEnabled);
        Assert.Equal(44, sut.ScheduleId);
    }

    [Fact]
    public void Set_helpers_delegate_to_matching_properties()
    {
        var sut = new SchedulePropertyManager(new FakeAppState(new ApplicationState()), TestLogging.CreateLogger());

        sut.SetIsBusy(false);
        sut.SetIsNewSchedule(true);
        sut.SetIsSchedulePageOverlayVisible(false);

        Assert.False(sut.IsBusy);
        Assert.True(sut.IsNewSchedule);
        Assert.False(sut.IsSchedulePageOverlayVisible);
    }
}
