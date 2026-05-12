#nullable enable

using Bible.Alarm.Stores;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Schedule.MusicSelectionContainer;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers.MusicSelection;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class MusicStateInitializerTests
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
        public void Dispatch(object action) =>
            throw new InvalidOperationException("InitializeFromState with null schedule must not dispatch.");

#pragma warning disable CS0067
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067
    }

    [Fact]
    public void InitializeFromState_when_current_schedule_is_null_returns_new_schedule_tuple_without_side_effects()
    {
        var state = new FakeAppState(new ApplicationState { CurrentSchedule = null });
        var dispatcher = new RecordingDispatcher();
        var media = new IdleCatalogMediaService();
        var display = new MusicDisplayTextProvider(state, media, TestLogging.CreateLogger());
        var notifier = new MusicPropertyNotifier(_ => { }, display);
        var sut = new MusicStateInitializer(state, dispatcher, display, notifier);

        var result = sut.InitializeFromState();

        Assert.Equal((0, true, (bool?)null), result);
        Assert.False(sut.HasSignaledReady);
    }
}
