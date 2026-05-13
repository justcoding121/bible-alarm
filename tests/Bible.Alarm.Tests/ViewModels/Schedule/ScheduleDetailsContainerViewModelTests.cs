#nullable enable

using AutoMapper;
using Bible.Alarm.Stores;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Schedule;
using Fluxor;
using Microsoft.Extensions.Logging.Abstractions;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class ScheduleDetailsContainerViewModelTests
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

    private sealed class FakeAppState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    [Fact]
    public void Ctor_does_not_require_current_schedule()
    {
        ScheduleDetailsContainerViewModel? sut = null;
        try
        {
            var mapper = new MapperConfiguration(_ => { }, NullLoggerFactory.Instance).CreateMapper();
            sut = new ScheduleDetailsContainerViewModel(
                TestLogging.CreateLogger(),
                new FakeAppState(new ApplicationState()),
                new NopDispatcher(),
                mapper,
                null!);
            Assert.NotNull(sut);
        }
        finally
        {
            sut?.Dispose();
        }
    }
}
