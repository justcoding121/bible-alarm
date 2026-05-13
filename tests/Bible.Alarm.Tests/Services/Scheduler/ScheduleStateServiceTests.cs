#nullable enable

using Bible.Alarm.Services.Scheduler;
using Bible.Alarm.Tests.Support;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class ScheduleStateServiceTests
{
#pragma warning disable CS0067
    private sealed class NopDispatcher : IDispatcher
    {
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
        }
    }
#pragma warning restore CS0067

    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var deps = new ScheduleStateServiceDeps(
            TestLogging.CreateLogger(),
            null!,
            null!,
            null!,
            null!,
            new NopDispatcher(),
            null!,
            null!);

        var sut = new ScheduleStateService(deps);

        Assert.NotNull(sut);
    }
}
