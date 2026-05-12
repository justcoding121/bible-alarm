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

public sealed class ScheduleListItemStateHandlerTests
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
            CurrentPlayItem = PlayType.Bible
        };

    [Fact]
    public void HandleApplicationStateChanged_returns_null_when_schedule_id_not_positive()
    {
        var mapper = CreateMapper();
        var sut = new ScheduleListItemStateHandler(
            TestLogging.CreateLogger(),
            mapper,
            new FakeApplicationState(new ApplicationState()));

        var current = mapper.Map<AlarmSchedule>(StateItem(1, "One"));

        Assert.Null(sut.HandleApplicationStateChanged(0, current));
    }

    [Fact]
    public void HandleApplicationStateChanged_returns_null_when_current_schedule_null()
    {
        var sut = new ScheduleListItemStateHandler(
            TestLogging.CreateLogger(),
            CreateMapper(),
            new FakeApplicationState(new ApplicationState()));

        Assert.Null(sut.HandleApplicationStateChanged(3, null));
    }

    [Fact]
    public void HandleApplicationStateChanged_returns_null_when_schedule_missing_from_fluxor_state()
    {
        var mapper = CreateMapper();
        var sut = new ScheduleListItemStateHandler(
            TestLogging.CreateLogger(),
            mapper,
            new FakeApplicationState(new ApplicationState(new ObservableHashSet<ScheduleStateItem>())));

        var current = mapper.Map<AlarmSchedule>(StateItem(8, "Eight"));

        Assert.Null(sut.HandleApplicationStateChanged(8, current));
    }

    [Fact]
    public void HandleApplicationStateChanged_sets_NameChanged_when_title_differs()
    {
        var mapper = CreateMapper();
        var id = 12;
        var updated = StateItem(id, "After");
        var schedules = new ObservableHashSet<ScheduleStateItem> { updated };
        var sut = new ScheduleListItemStateHandler(
            TestLogging.CreateLogger(),
            mapper,
            new FakeApplicationState(new ApplicationState(schedules)));

        var current = mapper.Map<AlarmSchedule>(StateItem(id, "Before"));

        var info = sut.HandleApplicationStateChanged(id, current);

        Assert.NotNull(info);
        Assert.True(info.NameChanged);
    }
}
