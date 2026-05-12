#nullable enable

using AutoMapper;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.ViewModels;
using Bible.Alarm.ViewModels.HomeViewModelHelpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bible.Alarm.Tests;

public sealed class HomeStateChangeHandlerCtorDepsTests
{
    private sealed class NullServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private static IMapper MinimalMapper()
    {
        var cfg = new MapperConfiguration(_ => { }, NullLoggerFactory.Instance);
        return cfg.CreateMapper();
    }

    [Fact]
    public void HomeStateChangeHandlerDeps_equals_when_components_match()
    {
        var logger = TestLogging.CreateLogger();
        var preparer = new ScheduleDataPreparer(MinimalMapper());
        var manager = new ScheduleViewModelManager(logger, new NullServiceProvider(), _ => { });

        var a = new HomeStateChangeHandlerDeps(logger, preparer, manager);
        var b = new HomeStateChangeHandlerDeps(logger, preparer, manager);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void HomeStateChangeHandlerDeps_with_keeps_most_fields()
    {
        var logger = TestLogging.CreateLogger();
        var preparer = new ScheduleDataPreparer(MinimalMapper());
        var manager = new ScheduleViewModelManager(logger, new NullServiceProvider(), _ => { });

        var original = new HomeStateChangeHandlerDeps(logger, preparer, manager);
        var copy = original with { };

        Assert.Equal(original, copy);
    }

    [Fact]
    public void HomeStateChangeHandlerCallbacks_invoke_set_parts_and_reflect_getters()
    {
        var busyFlag = false;
        ObservableHashSet<ScheduleListItemViewModel>? currentSchedules = null;
        var notifierCalls = 0;
        var progressCalls = 0;

        var sut = new HomeStateChangeHandlerCallbacks(
            v => busyFlag = v,
            () => busyFlag,
            () => currentSchedules,
            s => currentSchedules = s,
            () => notifierCalls++,
            () => progressCalls++,
            static () => Task.CompletedTask,
            () => false);

        var replacement = new ObservableHashSet<ScheduleListItemViewModel>();
        sut.SetIsBusy(true);
        sut.SetSchedules(replacement);
        sut.NotifySchedulesChanged?.Invoke();
        sut.UpdateProgressBarVisibility();

        Assert.True(sut.GetIsBusy());
        Assert.Same(replacement, sut.GetSchedules());
        Assert.Equal(1, notifierCalls);
        Assert.Equal(1, progressCalls);
        Assert.False(sut.IsPlaybackModalVisible());
    }
}
