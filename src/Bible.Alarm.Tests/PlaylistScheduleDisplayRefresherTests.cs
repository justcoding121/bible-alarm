#nullable enable

using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Stores;
using Fluxor;
using Bible.Alarm.Tests.Support;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class PlaylistScheduleDisplayRefresherTests
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

    [Fact]
    public async Task RefreshAsync_no_op_when_alarm_or_display_service_missing()
    {
        var dispatcher = new RecordingDispatcher();
        var sut = new PlaylistScheduleDisplayRefresher(
            alarmScheduleService: null,
            scheduleDisplayNameService: null,
            new FakeApplicationState(new ApplicationState([])),
            dispatcher,
            TestLogging.CreateLogger());

        await sut.RefreshAsync(1, "E", "nwt", "gen", CancellationToken.None);

        Assert.Empty(dispatcher.Dispatched);
    }
}
