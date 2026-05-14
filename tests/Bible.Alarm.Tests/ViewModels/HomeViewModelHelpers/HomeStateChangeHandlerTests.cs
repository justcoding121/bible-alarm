#nullable enable

using AutoMapper;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels;
using Bible.Alarm.ViewModels.HomeViewModelHelpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bible.Alarm.Tests;

public sealed class HomeStateChangeHandlerTests
{
    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private static IMapper CreateMapper()
    {
        var cfg = new MapperConfiguration(cfg => cfg.AddProfile<ScheduleMappingProfile>(), NullLoggerFactory.Instance);
        return cfg.CreateMapper();
    }

    private static HomeStateChangeHandler CreateHandlerWithRealDeps(
        Action updateProgressBarVisibility,
        Func<ObservableHashSet<ScheduleListItemViewModel>?> getSchedules,
        Action<ObservableHashSet<ScheduleListItemViewModel>> setSchedules,
        Func<bool> isBusy,
        Action<bool> setIsBusy)
    {
        var dataPreparer = new ScheduleDataPreparer(CreateMapper());
        var viewModelManager = new ScheduleViewModelManager(TestLogging.CreateLogger(), new EmptyServiceProvider(), _ => { });
        var deps = new HomeStateChangeHandlerDeps(TestLogging.CreateLogger(), dataPreparer, viewModelManager);
        var callbacks = new HomeStateChangeHandlerCallbacks(
            SetIsBusy: setIsBusy,
            GetIsBusy: isBusy,
            GetSchedules: getSchedules,
            SetSchedules: setSchedules,
            NotifySchedulesChanged: null,
            UpdateProgressBarVisibility: updateProgressBarVisibility,
            FadeOutProgressBarAsync: () => Task.CompletedTask,
            IsPlaybackModalVisible: () => false);
        return new HomeStateChangeHandler(deps, callbacks);
    }

    [Fact]
    public async Task HandleStateChangedAsync_invokes_updateProgressBarVisibility_when_schedules_null()
    {
        var called = false;
        var deps = new HomeStateChangeHandlerDeps(TestLogging.CreateLogger(), null!, null!);
        var callbacks = new HomeStateChangeHandlerCallbacks(
            SetIsBusy: _ => { },
            GetIsBusy: () => false,
            GetSchedules: () => null,
            SetSchedules: _ => { },
            NotifySchedulesChanged: null,
            UpdateProgressBarVisibility: () => { called = true; },
            FadeOutProgressBarAsync: () => Task.CompletedTask,
            IsPlaybackModalVisible: () => false);
        var sut = new HomeStateChangeHandler(deps, callbacks);

        await sut.HandleStateChangedAsync(new ApplicationState { Schedules = null! });

        Assert.True(called);
    }

    [Fact]
    public async Task HandleStateChangedAsync_second_call_with_same_empty_schedules_returns_early()
    {
        ObservableHashSet<ScheduleListItemViewModel>? scheduleCollection = null;
        var sut = CreateHandlerWithRealDeps(
            updateProgressBarVisibility: () => { },
            getSchedules: () => scheduleCollection,
            setSchedules: s => { scheduleCollection = s; },
            isBusy: () => false,
            setIsBusy: _ => { });

        var emptySchedules = new ObservableHashSet<ScheduleStateItem>();
        var state = new ApplicationState { Schedules = emptySchedules };

        await sut.HandleStateChangedAsync(state);
        // Second call: handler detects nothing changed (UnchangedSinceLastProcessed = true) and exits early
        await sut.HandleStateChangedAsync(state);
    }

    [Fact]
    public async Task ApplyDeferredReorderAsync_is_noop_when_nothing_deferred()
    {
        var deps = new HomeStateChangeHandlerDeps(TestLogging.CreateLogger(), null!, null!);
        var callbacks = new HomeStateChangeHandlerCallbacks(
            SetIsBusy: _ => { },
            GetIsBusy: () => false,
            GetSchedules: () => null,
            SetSchedules: _ => { },
            NotifySchedulesChanged: null,
            UpdateProgressBarVisibility: () => { },
            FadeOutProgressBarAsync: () => Task.CompletedTask,
            IsPlaybackModalVisible: () => false);
        var sut = new HomeStateChangeHandler(deps, callbacks);

        await sut.ApplyDeferredReorderAsync();
    }
}
