#nullable enable

using AutoMapper;
using Bible.Alarm.Stores;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Schedule.MusicSelectionContainer;
using Fluxor;
using Microsoft.Extensions.Logging.Abstractions;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class MusicStateChangeHandlerCtorDepsTests
{
    private static MapperConfiguration MapperConfig() =>
        new(cfg => { }, NullLoggerFactory.Instance);

    private sealed class RecordingDispatcher : IDispatcher
    {
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action) =>
            ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(action));
    }

    private sealed class RecordingState(ApplicationState snapshot) : IState<ApplicationState>
    {
#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067

        public ApplicationState Value => snapshot;
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private static MusicStateChangeHandlerServices BuildUniqueServices() =>
        new MusicStateChangeHandlerServices(
            TestLogging.CreateLogger(),
            new RecordingState(new ApplicationState()),
            new RecordingDispatcher(),
            MapperConfig().CreateMapper(),
            new EmptyServiceProvider());

    private static MusicStateChangeHandlerCollaborators CollaboratorsUsing(MusicStateTracker tracker) =>
        new MusicStateChangeHandlerCollaborators(
            tracker,
            PropertyNotifier: null!,
            DisplayTextProvider: null!);

    [Fact]
    public void MusicStateChangeHandlerServices_equal_when_components_share_instances()
    {
        var logger = TestLogging.CreateLogger();
        var state = new RecordingState(new ApplicationState());
        var dispatcher = new RecordingDispatcher();
        var mapper = MapperConfig().CreateMapper();
        var provider = new EmptyServiceProvider();

        var a = new MusicStateChangeHandlerServices(logger, state, dispatcher, mapper, provider);
        var b = new MusicStateChangeHandlerServices(logger, state, dispatcher, mapper, provider);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void MusicStateChangeHandlerServices_not_equal_when_mapper_instance_differs()
    {
        var baseServices = BuildUniqueServices();
        var altered = baseServices with { Mapper = MapperConfig().CreateMapper() };

        Assert.NotEqual(baseServices, altered);
    }

    [Fact]
    public void MusicStateChangeHandlerCollaborators_equal_when_tracker_and_sentinels_match()
    {
        var tracker = new MusicStateTracker();

        var a = CollaboratorsUsing(tracker);
        var b = CollaboratorsUsing(tracker);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void MusicStateChangeHandlerCollaborators_not_equal_when_tracker_replaced()
    {
        var first = CollaboratorsUsing(new MusicStateTracker());
        var second = CollaboratorsUsing(new MusicStateTracker());

        Assert.NotEqual(first, second);
    }
}
