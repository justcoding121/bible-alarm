#nullable enable

using AutoMapper;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Stores.Models;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bible.Alarm.Tests;

public sealed class ScheduleCreateHandlerTests
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
        public Task PopulateDisplayNamesAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule) =>
            Task.CompletedTask;
    }

    private static IMapper CreateMapper()
    {
        var cfg = new MapperConfiguration(
            cfg => cfg.AddProfile<ScheduleMappingProfile>(),
            NullLoggerFactory.Instance);
        return cfg.CreateMapper();
    }

    [Fact]
    public async Task HandleAsync_skips_when_schedule_null_and_service_unavailable()
    {
        var sut = new ScheduleCreateHandler(CreateMapper(), null, null, new RecordingScheduleDisplayNameService());
        var dispatcher = new RecordingDispatcher();

        await sut.HandleAsync(new CreateScheduleAction(null!), dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleAsync_dispatches_failure_when_alarm_schedule_service_unavailable()
    {
        var sut = new ScheduleCreateHandler(CreateMapper(), null, null, new RecordingScheduleDisplayNameService());
        var dispatcher = new RecordingDispatcher();
        var vm = new ScheduleStateItem { Id = 0, Name = "Draft" };

        await sut.HandleAsync(new CreateScheduleAction(vm), dispatcher);

        var fail = Assert.Single(dispatcher.Dispatched);
        var action = Assert.IsType<CreateScheduleFailureAction>(fail);
        Assert.Same(vm, action.Schedule);
        Assert.Equal("Service unavailable", action.Error);
    }
}
