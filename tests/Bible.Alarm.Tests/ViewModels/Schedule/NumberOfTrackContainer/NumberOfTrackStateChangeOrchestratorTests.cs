#nullable enable

using System.Reflection;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Schedule;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class NumberOfTrackStateChangeOrchestratorTests
{
    private sealed class FakeApplicationState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

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

    private static ContainerReadySignaler CreateSignaler(ApplicationState state)
    {
        var fakeState = new FakeApplicationState(state);
        var dispatcher = new RecordingDispatcher();
        return new ContainerReadySignaler(fakeState, dispatcher, "NumberOfTrack", s => s.ContainerReadiness.NumberOfTrack);
    }

    private static void SetHasSignaledReady(ContainerReadySignaler signaler, bool value)
    {
        var field = typeof(ContainerReadySignaler).GetField("hasSignaledReady", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(signaler, value);
    }

    [Fact]
    public void Reset_when_container_signaled_ready_but_number_track_not_marked_ready()
    {
        var state = new ApplicationState
        {
            CurrentSchedule = new ScheduleStateItem { Id = 5 },
            ContainerReadiness = ContainerReadiness.NotReady,
        };
        var signaler = CreateSignaler(state);
        SetHasSignaledReady(signaler, true);

        var (shouldReset, shouldReinit) = NumberOfTrackContainerViewModel.GetReinitDecision(signaler, state, scheduleId: 5);

        Assert.True(shouldReset);
        Assert.True(shouldReinit);
    }

    [Fact]
    public void Init_when_new_schedule_and_ready_not_yet_signaled()
    {
        var state = new ApplicationState
        {
            CurrentSchedule = new ScheduleStateItem { Id = 0 },
            ContainerReadiness = ContainerReadiness.NotReady,
        };
        var signaler = CreateSignaler(state);

        var (shouldReset, shouldReinit) = NumberOfTrackContainerViewModel.GetReinitDecision(signaler, state, scheduleId: 0);

        Assert.False(shouldReset);
        Assert.True(shouldReinit);
    }

    [Fact]
    public void Reinit_when_editing_schedule_changes_under_container()
    {
        var state = new ApplicationState
        {
            CurrentSchedule = new ScheduleStateItem { Id = 99 },
            ContainerReadiness = ContainerReadiness.NotReady,
        };
        var signaler = CreateSignaler(state);

        var (shouldReset, shouldReinit) = NumberOfTrackContainerViewModel.GetReinitDecision(signaler, state, scheduleId: 4);

        Assert.True(shouldReset);
        Assert.True(shouldReinit);
    }

    [Fact]
    public void No_work_when_stable_same_schedule_and_signaler_idle()
    {
        var state = new ApplicationState
        {
            CurrentSchedule = new ScheduleStateItem { Id = 7 },
            ContainerReadiness = new ContainerReadiness { NumberOfTrack = true },
        };
        var signaler = CreateSignaler(state);

        var (shouldReset, shouldReinit) = NumberOfTrackContainerViewModel.GetReinitDecision(signaler, state, scheduleId: 7);

        Assert.False(shouldReset);
        Assert.False(shouldReinit);
    }
}
