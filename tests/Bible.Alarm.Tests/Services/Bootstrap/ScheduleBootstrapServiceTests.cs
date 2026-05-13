#nullable enable

using AutoMapper;
using Bible.Alarm.Services.Bootstrap;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bible.Alarm.Tests;

public sealed class ScheduleBootstrapServiceTests
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
        var mapper = new MapperConfiguration(_ => { }, NullLoggerFactory.Instance).CreateMapper();
        var populatorDeps = new ScheduleStatePopulatorDeps(
            null,
            null,
            mapper,
            null,
            null,
            null,
            null!);

        var sut = new ScheduleBootstrapService(
            null!,
            null!,
            new NopDispatcher(),
            populatorDeps);

        Assert.NotNull(sut);
    }
}
