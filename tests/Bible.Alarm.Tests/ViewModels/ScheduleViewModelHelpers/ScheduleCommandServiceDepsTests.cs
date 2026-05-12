#nullable enable

using AutoMapper;
using Bible.Alarm.Stores;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;
using Fluxor;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bible.Alarm.Tests;

public sealed class ScheduleCommandServiceDepsTests
{
    private static MapperConfiguration MapperConfig() =>
        new(cfg => { }, NullLoggerFactory.Instance);

    [Fact]
    public void ScheduleCommandServiceDeps_equals_when_components_share_instances()
    {
        var logger = TestLogging.CreateLogger();
        var dispatcher = new RecordingDispatcher();
        var state = new RecordingState(new ApplicationState());
        var mapper = MapperConfig().CreateMapper();

        var a = new ScheduleCommandServiceDeps(
            logger,
            dispatcher,
            null!,
            null!,
            null!,
            null!,
            null!,
            mapper,
            state);

        var b = new ScheduleCommandServiceDeps(
            logger,
            dispatcher,
            null!,
            null!,
            null!,
            null!,
            null!,
            mapper,
            state);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ScheduleCommandService_deps_with_substitutes_mapper_breaks_equivalence()
    {
        var baseDeps = BuildUniqueDeps();
        var altered = baseDeps with { Mapper = MapperConfig().CreateMapper() };

        Assert.NotEqual(baseDeps, altered);
    }

    [Fact]
    public void ScheduleCommandService_can_be_constructed_from_deps_bundle()
    {
        var sut = new ScheduleCommandService(BuildUniqueDeps());

        Assert.NotNull(sut);
    }

    private static ScheduleCommandServiceDeps BuildUniqueDeps() =>
        new(
            TestLogging.CreateLogger(),
            new RecordingDispatcher(),
            null!,
            null!,
            null!,
            null!,
            null!,
            MapperConfig().CreateMapper(),
            new RecordingState(new ApplicationState()));

    private sealed class RecordingDispatcher : Fluxor.IDispatcher
    {
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action) =>
            ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(action));
    }

    private sealed class RecordingState(ApplicationState snapshot) : Fluxor.IState<ApplicationState>
    {
#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067

        public ApplicationState Value => snapshot;
    }
}
