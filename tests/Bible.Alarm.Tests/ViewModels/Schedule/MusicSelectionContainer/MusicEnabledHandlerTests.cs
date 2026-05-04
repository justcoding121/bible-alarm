#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Effects.Services;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Schedule.MusicSelectionContainer;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class MusicEnabledHandlerTests
{
    private sealed class FakeAppState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

        public void Dispatch(object action) => Dispatched.Add(action);

#pragma warning disable CS0067
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067
    }

    [Fact]
    public void HandleSetMusicEnabled_returns_false_when_current_schedule_missing()
    {
        var dispatcher = new RecordingDispatcher();
        var state = new FakeAppState(new ApplicationState { CurrentSchedule = null });
        var handler = new MusicEnabledHandler(
            TestLogging.CreateLogger(),
            dispatcher,
            new ServiceCollection().BuildServiceProvider(),
            state);

        var pendingToggles = 0;
        Assert.False(handler.HandleSetMusicEnabled(true, false, false, null, _ => pendingToggles++, _ => { }, () => { }));

        Assert.Equal(0, pendingToggles);
        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public void HandleSetMusicEnabled_returns_false_when_value_unchanged()
    {
        var schedule = new ScheduleStateItem { Id = 1 };
        var dispatcher = new RecordingDispatcher();
        var state = new FakeAppState(new ApplicationState { CurrentSchedule = schedule });
        var handler = new MusicEnabledHandler(
            TestLogging.CreateLogger(),
            dispatcher,
            new ServiceCollection().BuildServiceProvider(),
            state);

        Assert.False(handler.HandleSetMusicEnabled(false, false, false, null, _ => { }, _ => { }, () => { }));

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public void HandleSetMusicEnabled_while_syncing_from_state_clears_pending_and_invokes_property_changed()
    {
        var schedule = new ScheduleStateItem { Id = 1 };
        var dispatcher = new RecordingDispatcher();
        var state = new FakeAppState(new ApplicationState { CurrentSchedule = schedule });
        var handler = new MusicEnabledHandler(
            TestLogging.CreateLogger(),
            dispatcher,
            new ServiceCollection().BuildServiceProvider(),
            state);

        var pending = true;
        var propCalls = 0;
        Assert.False(handler.HandleSetMusicEnabled(
            true,
            currentValue: false,
            isUpdatingFromState: true,
            initialMusicEnabledOnPageLoad: null,
            v => pending = v,
            _ => { },
            () => propCalls++));

        Assert.False(pending);
        Assert.Equal(1, propCalls);
        Assert.Empty(dispatcher.Dispatched);
    }
}
