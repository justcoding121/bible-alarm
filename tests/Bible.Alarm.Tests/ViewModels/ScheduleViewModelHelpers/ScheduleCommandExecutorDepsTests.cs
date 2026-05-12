#nullable enable

using AutoMapper;
using Bible.Alarm.Stores;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Schedule;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;
using Fluxor;
using Microsoft.Extensions.Logging.Abstractions;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class ScheduleCommandExecutorDepsTests
{
    private static MapperConfiguration MapperConfig() =>
        new(cfg => { }, NullLoggerFactory.Instance);

    private sealed class RecordingDispatcher : IDispatcher
    {
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action) =>
            ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(action));
    }

    private sealed class RecordingApplicationState(ApplicationState snapshot) : IState<ApplicationState>
    {
#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067

        public ApplicationState Value => snapshot;
    }

    private sealed class RecordingPlaybackState(PlaybackState snapshot) : IState<PlaybackState>
    {
#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067

        public PlaybackState Value => snapshot;
    }

    [Fact]
    public void ScheduleCommandExecutorCoreDeps_equals_when_bundle_shares_instances()
    {
        var dispatcher = new RecordingDispatcher();
        var application = new RecordingApplicationState(new ApplicationState());
        var playback = new RecordingPlaybackState(new PlaybackState());
        var mapper = MapperConfig().CreateMapper();

        var logger = TestLogging.CreateLogger();

        var a = new ScheduleCommandExecutorCoreDeps(
            null!,
            null!,
            application,
            playback,
            dispatcher,
            mapper,
            logger);

        var b = new ScheduleCommandExecutorCoreDeps(
            null!,
            null!,
            application,
            playback,
            dispatcher,
            mapper,
            logger);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ScheduleCommandExecutorCoreDeps_substitute_mapper_changes_equivalence()
    {
        var baseline = BuildUniqueCoreDeps();

        Assert.NotEqual(
            baseline,
            baseline with { Mapper = MapperConfig().CreateMapper() });
    }

    [Fact]
    public void ScheduleCommandExecutorUiHooks_equal_when_delegate_instances_match()
    {
        Func<MusicSelectionContainerViewModel?> getMusic = () => null;

        var a = new ScheduleCommandExecutorUiHooks(getMusic, null, null, null, null, null, null);
        var b = new ScheduleCommandExecutorUiHooks(getMusic, null, null, null, null, null, null);

        Assert.Equal(a, b);

        Func<MusicSelectionContainerViewModel?> other = () => null;

        Assert.NotEqual(a, new ScheduleCommandExecutorUiHooks(other, null, null, null, null, null, null));
    }

    private static ScheduleCommandExecutorCoreDeps BuildUniqueCoreDeps() =>
        new(
            null!,
            null!,
            new RecordingApplicationState(new ApplicationState()),
            new RecordingPlaybackState(new PlaybackState()),
            new RecordingDispatcher(),
            MapperConfig().CreateMapper(),
            TestLogging.CreateLogger());
}
