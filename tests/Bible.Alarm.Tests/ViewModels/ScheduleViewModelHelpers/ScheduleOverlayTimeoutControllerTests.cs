#nullable enable

using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class ScheduleOverlayTimeoutControllerTests
{
    private sealed class MutableState : IState<ApplicationState>
    {
        public required ApplicationState Current { get; set; }

        public ApplicationState Value => Current;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

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

    [Fact]
    public void HandleStateChanged_returns_immediately_when_overlay_stays_hidden()
    {
        var inner = new ApplicationState(new ObservableHashSet<ScheduleStateItem>());
        var state = new MutableState { Current = inner };
        var dispatcher = new RecordingDispatcher();
        using var sut = new ScheduleOverlayTimeoutController(TestLogging.CreateLogger(), state, dispatcher);

        sut.HandleStateChanged(inner);
        sut.HandleStateChanged(inner);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public void OnContentLoaded_dispatches_hide_when_overlay_visible_and_all_containers_ready()
    {
        var inner = new ApplicationState(
            new ObservableHashSet<ScheduleStateItem>(),
            isSchedulePageOverlayVisible: true,
            containerReadiness: ContainerReadiness.AllContainersReady);
        var state = new MutableState { Current = inner };
        var dispatcher = new RecordingDispatcher();
        using var sut = new ScheduleOverlayTimeoutController(TestLogging.CreateLogger(), state, dispatcher);

        sut.OnContentLoaded();

        var action = Assert.Single(dispatcher.Dispatched);
        var overlay = Assert.IsType<SetSchedulePageOverlayAction>(action);
        Assert.False(overlay.IsVisible);
    }
}
