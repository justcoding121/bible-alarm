#nullable enable

using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Schedule;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class ContainerReadySignalerTests
{
    private sealed class FakeState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class ThrowingDispatcher : IDispatcher
    {
        public void Dispatch(object action) =>
            throw new InvalidOperationException($"Unexpected dispatch: {action.GetType().Name}");

#pragma warning disable CS0067
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067
    }

    [Fact]
    public void TrySignalReady_when_state_already_ready_returns_without_dispatch()
    {
        var readiness = ContainerReadiness.AllContainersReady;
        var state = new FakeState(new ApplicationState([], null, false, false, readiness));
        var dispatcher = new ThrowingDispatcher();
        var sut = new ContainerReadySignaler(state, dispatcher, "X", s => s.ContainerReadiness.MusicSelection);

        sut.TrySignalReady();

        Assert.False(sut.HasSignaledReady);
    }

    [Fact]
    public void TrySignalReady_when_isReady_flips_true_after_first_check_sets_HasSignaledReady_without_dispatch()
    {
        var state = new FakeState(new ApplicationState());
        var dispatcher = new ThrowingDispatcher();
        var calls = 0;
        bool IsReady(ApplicationState _) => ++calls >= 2;

        var sut = new ContainerReadySignaler(state, dispatcher, "X", IsReady);

        sut.TrySignalReady();

        Assert.Equal(2, calls);
        Assert.True(sut.HasSignaledReady);
    }

    [Fact]
    public void Reset_clears_signaled_flags()
    {
        var state = new FakeState(new ApplicationState());
        var dispatcher = new ThrowingDispatcher();
        var calls = 0;
        bool IsReady(ApplicationState _) => ++calls >= 2;
        var sut = new ContainerReadySignaler(state, dispatcher, "Y", IsReady);
        sut.TrySignalReady();
        Assert.True(sut.HasSignaledReady);

        sut.Reset();

        Assert.False(sut.HasSignaledReady);
    }

    [Fact]
    public void TrySignalReady_when_state_reports_ready_after_flags_set_skips_dispatch()
    {
        var state = new FakeState(new ApplicationState());
        var dispatcher = new ThrowingDispatcher();
        var calls = 0;
        bool IsReady(ApplicationState _) => ++calls >= 2;

        var sut = new ContainerReadySignaler(state, dispatcher, "X", IsReady);

        sut.TrySignalReady();

        Assert.True(sut.HasSignaledReady);
    }

    [Collection("MauiUi")]
    public sealed class MauiContainerReadySignalerTests(MauiUiFixture fixture)
    {
        [Fact]
        public void TrySignalReady_dispatches_container_ready_action_on_main_thread()
        {
            if (!MauiUiTestBootstrap.IsReady)
            {
                return;
            }

            _ = fixture;

            var state = new FakeState(new ApplicationState());
            var dispatched = new List<object>();
            var dispatcher = new RecordingDispatcher(dispatched);
            var sut = new ContainerReadySignaler(state, dispatcher, "MusicSelection", _ => false);
            var signaled = new ManualResetEventSlim(false);
            dispatcher.OnDispatched = () => signaled.Set();

            sut.TrySignalReady();

            Assert.True(signaled.Wait(TimeSpan.FromSeconds(5)));
            Assert.IsType<ContainerReadyAction>(Assert.Single(dispatched));
            Assert.Equal("MusicSelection", ((ContainerReadyAction)dispatched[0]).ContainerName);
            Assert.True(sut.HasSignaledReady);
        }

        private sealed class RecordingDispatcher(List<object> dispatched) : IDispatcher
        {
            public Action? OnDispatched { get; set; }

#pragma warning disable CS0067
            public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067

            public void Dispatch(object action)
            {
                dispatched.Add(action);
                OnDispatched?.Invoke();
            }
        }
    }
}
