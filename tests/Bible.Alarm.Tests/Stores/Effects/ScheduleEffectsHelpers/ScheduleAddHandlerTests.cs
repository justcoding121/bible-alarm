#nullable enable

using AutoMapper;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Stores.Models;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bible.Alarm.Tests;

public sealed class ScheduleAddHandlerTests
{
    private sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
            Dispatched.Add(action);
            ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(action));
        }
    }

    private sealed class RecordingScheduleDisplayNameService : IScheduleDisplayNameService
    {
        public List<(ScheduleStateItem Dto, AlarmSchedule Entity)> PopulateCalls { get; } = [];

        public Task PopulateDisplayNamesAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
        {
            PopulateCalls.Add((scheduleStateItem, schedule));
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingScheduleDisplayNameService : IScheduleDisplayNameService
    {
        public Task PopulateDisplayNamesAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule) =>
            throw new InvalidOperationException("test");
    }

    private static IMapper CreateMapper()
    {
        var cfg = new MapperConfiguration(
            cfg => cfg.AddProfile<ScheduleMappingProfile>(),
            NullLoggerFactory.Instance);
        return cfg.CreateMapper();
    }

    private static AlarmSchedule Alarm(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            IsEnabled = true,
            Hour = 8,
            Minute = 30,
            Second = 0,
            DaysOfWeek = WeekDays.Tuesday,
            NotificationEnabled = true,
            MusicEnabled = false,
        };

    [Fact]
    public async Task HandleAsync_skips_when_schedule_null()
    {
        var displayNames = new RecordingScheduleDisplayNameService();
        var sut = new ScheduleAddHandler(CreateMapper(), displayNames);
        var dispatcher = new RecordingDispatcher();

        await sut.HandleAsync(new AddScheduleAction(null!), dispatcher);

        Assert.Empty(dispatcher.Dispatched);
        Assert.Empty(displayNames.PopulateCalls);
    }

    [Fact]
    public async Task HandleAsync_maps_populates_display_names_and_dispatches_success()
    {
        var displayNames = new RecordingScheduleDisplayNameService();
        var mapper = CreateMapper();
        var sut = new ScheduleAddHandler(mapper, displayNames);
        var dispatcher = new RecordingDispatcher();
        var entity = Alarm(11, "Morning");

        await sut.HandleAsync(new AddScheduleAction(entity), dispatcher);

        var success = Assert.Single(dispatcher.Dispatched);
        var action = Assert.IsType<AddScheduleSuccessAction>(success);
        Assert.Equal(11, action.Schedule.Id);
        Assert.Equal("Morning", action.Schedule.Name);

        var call = Assert.Single(displayNames.PopulateCalls);
        Assert.Same(action.Schedule, call.Dto);
        Assert.Same(entity, call.Entity);
    }

    [Fact]
    public async Task HandleAsync_swallows_exception_when_populate_display_names_fails()
    {
        var sut = new ScheduleAddHandler(CreateMapper(), new ThrowingScheduleDisplayNameService());
        var dispatcher = new RecordingDispatcher();

        await sut.HandleAsync(new AddScheduleAction(Alarm(7, "E")), dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }
}
