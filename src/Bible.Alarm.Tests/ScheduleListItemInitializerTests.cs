#nullable enable

using AutoMapper;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers;
using Fluxor;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bible.Alarm.Tests;

public sealed class ScheduleListItemInitializerTests
{
    private sealed class FakeApplicationState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private static IMapper CreateMapper()
    {
        var cfg = new MapperConfiguration(
            cfg => cfg.AddProfile<ScheduleMappingProfile>(),
            NullLoggerFactory.Instance);
        return cfg.CreateMapper();
    }

    private static ScheduleStateItem StateItem(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            IsEnabled = true,
            Hour = 7,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = false,
            SnoozeMinutes = 5,
            NumberOfTracksToPlay = 1,
            AlwaysPlayFromStart = false,
            CurrentPlayItem = PlayType.Bible,
        };

    private static AlarmSchedule Alarm(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            IsEnabled = true,
            Hour = 7,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = false,
        };

    [Fact]
    public void InitializeFromSchedule_returns_nulls_when_schedule_null()
    {
        var sut = new ScheduleListItemInitializer(
            TestLogging.CreateLogger(),
            CreateMapper(),
            new FakeApplicationState(new ApplicationState()));

        var (schedule, item) = sut.InitializeFromSchedule(null!);

        Assert.Null(schedule);
        Assert.Null(item);
    }

    [Fact]
    public void InitializeFromSchedule_returns_nulls_when_schedule_id_non_positive()
    {
        var sut = new ScheduleListItemInitializer(
            TestLogging.CreateLogger(),
            CreateMapper(),
            new FakeApplicationState(new ApplicationState()));

        var (schedule, item) = sut.InitializeFromSchedule(new AlarmSchedule { Id = 0, Name = "x" });

        Assert.Null(schedule);
        Assert.Null(item);
    }

    [Fact]
    public void InitializeFromSchedule_uses_explicit_state_item_when_provided()
    {
        var sut = new ScheduleListItemInitializer(
            TestLogging.CreateLogger(),
            CreateMapper(),
            new FakeApplicationState(new ApplicationState()));
        var alarm = Alarm(3, "Three");
        var stateItem = StateItem(3, "Three");

        var (schedule, item) = sut.InitializeFromSchedule(alarm, stateItem);

        Assert.Same(alarm, schedule);
        Assert.Same(stateItem, item);
    }

    [Fact]
    public void InitializeFromSchedule_resolves_state_item_from_application_state_when_omitted()
    {
        var stateItem = StateItem(9, "Nine");
        var set = new ObservableHashSet<ScheduleStateItem> { stateItem };
        var sut = new ScheduleListItemInitializer(
            TestLogging.CreateLogger(),
            CreateMapper(),
            new FakeApplicationState(new ApplicationState(set)));
        var alarm = Alarm(9, "Nine");

        var (schedule, item) = sut.InitializeFromSchedule(alarm);

        Assert.Same(alarm, schedule);
        Assert.Same(stateItem, item);
    }

    [Fact]
    public void SetScheduleId_returns_nulls_when_id_non_positive()
    {
        var sut = new ScheduleListItemInitializer(
            TestLogging.CreateLogger(),
            CreateMapper(),
            new FakeApplicationState(new ApplicationState()));

        var (schedule, item) = sut.SetScheduleId(0);

        Assert.Null(schedule);
        Assert.Null(item);
    }

    [Fact]
    public void SetScheduleId_returns_nulls_when_schedule_missing_from_state()
    {
        var sut = new ScheduleListItemInitializer(
            TestLogging.CreateLogger(),
            CreateMapper(),
            new FakeApplicationState(new ApplicationState([])));

        var (schedule, item) = sut.SetScheduleId(404);

        Assert.Null(schedule);
        Assert.Null(item);
    }

    [Fact]
    public void SetScheduleId_maps_matching_state_item_to_alarm_schedule()
    {
        var stateItem = StateItem(2, "Two");
        var set = new ObservableHashSet<ScheduleStateItem> { stateItem };
        var sut = new ScheduleListItemInitializer(
            TestLogging.CreateLogger(),
            CreateMapper(),
            new FakeApplicationState(new ApplicationState(set)));

        var (schedule, item) = sut.SetScheduleId(2);

        Assert.NotNull(schedule);
        Assert.Equal(2, schedule!.Id);
        Assert.Equal("Two", schedule.Name);
        Assert.Same(stateItem, item);
    }
}
