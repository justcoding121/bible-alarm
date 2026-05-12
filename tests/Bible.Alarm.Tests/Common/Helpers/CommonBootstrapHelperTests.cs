#nullable enable

using AutoMapper;
using Bible.Alarm.Common.Helpers;
using Fluxor;
using Microsoft.Extensions.Logging.Abstractions;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class CommonBootstrapHelperTests
{
    private static MapperConfiguration MapperConfig() =>
        new(cfg => { }, NullLoggerFactory.Instance);

    private sealed class RecordingDispatcher : IDispatcher
    {
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action) =>
            ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(action));
    }

    [Fact]
    public void BootstrapServices_equal_when_dependency_graph_matches()
    {
        var dispatcher = new RecordingDispatcher();
        var mapper = MapperConfig().CreateMapper();

        var a = new CommonBootstrapHelper.BootstrapServices(
            null!,
            null!,
            null!,
            dispatcher,
            BiblePublicationService: null,
            BiblePublicationSectionService: null,
            mapper,
            MediaService: null,
            MelodyMusicService: null);

        var b = new CommonBootstrapHelper.BootstrapServices(
            null!,
            null!,
            null!,
            dispatcher,
            BiblePublicationService: null,
            BiblePublicationSectionService: null,
            mapper,
            MediaService: null,
            MelodyMusicService: null);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void BootstrapServices_not_equal_when_mapper_instance_differs()
    {
        var dispatcher = new RecordingDispatcher();
        var baseline = new CommonBootstrapHelper.BootstrapServices(
            null!,
            null!,
            null!,
            dispatcher,
            null,
            null,
            MapperConfig().CreateMapper(),
            null,
            null);

        Assert.NotEqual(
            baseline,
            baseline with { Mapper = MapperConfig().CreateMapper() });
    }
}
