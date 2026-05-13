#nullable enable

using AutoMapper;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels;
using Fluxor;
using Microsoft.Extensions.Logging.Abstractions;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class ScheduleListItemViewModelTests
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

    private sealed class FakeApplicationState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class FakePlaybackState(PlaybackState value) : IState<PlaybackState>
    {
        public PlaybackState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private static IMapper CreateMapper()
    {
        var cfg = new MapperConfiguration(
            c => c.AddProfile<ScheduleMappingProfile>(),
            NullLoggerFactory.Instance);
        return cfg.CreateMapper();
    }

    [Fact]
    public void Ctor_builds_with_minimal_dependencies()
    {
        ScheduleListItemViewModel? sut = null;
        try
        {
            var deps = new ScheduleListItemViewModelDeps(
                TestLogging.CreateLogger(),
                PlaybackService: null!,
                StopPlaybackService: null!,
                ScheduleStateService: null!,
                ApplicationState: new FakeApplicationState(new ApplicationState()),
                PlaybackState: new FakePlaybackState(new PlaybackState()),
                Dispatcher: new NopDispatcher(),
                Mapper: CreateMapper(),
                CategoryNameService: null!);

            sut = new ScheduleListItemViewModel(deps);
            Assert.NotNull(sut);
        }
        finally
        {
            sut?.Dispose();
        }
    }
}
