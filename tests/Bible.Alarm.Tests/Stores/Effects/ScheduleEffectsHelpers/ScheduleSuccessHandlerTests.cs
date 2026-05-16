#nullable enable

using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;
using Bible.Alarm.Stores.Models;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class ScheduleSuccessHandlerTests
{
    private sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

        public void Dispatch(object action) => Dispatched.Add(action);

#pragma warning disable CS0067
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067
    }

    [Fact]
    public async Task HandleUpdateScheduleSuccess_skips_car_play_dispatch_when_schedule_id_invalid()
    {
        var dispatcher = new RecordingDispatcher();

        await ScheduleSuccessHandler.HandleUpdateScheduleSuccess(
            new UpdateScheduleSuccessAction(new ScheduleStateItem { Id = 0 }),
            dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleUpdateScheduleSuccess_skips_car_play_dispatch_when_schedule_is_null()
    {
        var dispatcher = new RecordingDispatcher();

        await ScheduleSuccessHandler.HandleUpdateScheduleSuccess(
            new UpdateScheduleSuccessAction(null!),
            dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleUpdateScheduleSuccess_dispatches_set_car_play_when_schedule_id_positive()
    {
        var dispatcher = new RecordingDispatcher();

        await ScheduleSuccessHandler.HandleUpdateScheduleSuccess(
            new UpdateScheduleSuccessAction(new ScheduleStateItem { Id = 99 }),
            dispatcher);

        var action = Assert.Single(dispatcher.Dispatched);
        Assert.IsType<SetCarPlayScreenAction>(action);
    }

    [Fact]
    public async Task HandleRemoveScheduleSuccess_dispatches_set_car_play_screen()
    {
        var dispatcher = new RecordingDispatcher();

        await ScheduleSuccessHandler.HandleRemoveScheduleSuccess(new RemoveScheduleSuccessAction(404), dispatcher);

        var action = Assert.Single(dispatcher.Dispatched);
        Assert.IsType<SetCarPlayScreenAction>(action);
    }
}
