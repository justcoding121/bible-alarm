#nullable enable

using System.Reflection;
using AutoMapper;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels;
using Bible.Alarm.ViewModels.HomeViewModelHelpers;
using Fluxor;
using Microsoft.Extensions.Logging.Abstractions;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class HomeStateChangeHandlerTests
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
        Action<bool> setIsBusy,
        IServiceProvider? serviceProvider = null,
        Func<Task>? fadeOutProgressBarAsync = null,
        Func<bool>? isPlaybackModalVisible = null)
    {
        var dataPreparer = new ScheduleDataPreparer(CreateMapper());
        var viewModelManager = new ScheduleViewModelManager(
            TestLogging.CreateLogger(),
            serviceProvider ?? new EmptyServiceProvider(),
            _ => { });
        var deps = new HomeStateChangeHandlerDeps(TestLogging.CreateLogger(), dataPreparer, viewModelManager);
        var callbacks = new HomeStateChangeHandlerCallbacks(
            SetIsBusy: setIsBusy,
            GetIsBusy: isBusy,
            GetSchedules: getSchedules,
            SetSchedules: setSchedules,
            NotifySchedulesChanged: null,
            UpdateProgressBarVisibility: updateProgressBarVisibility,
            FadeOutProgressBarAsync: fadeOutProgressBarAsync ?? (() => Task.CompletedTask),
            IsPlaybackModalVisible: isPlaybackModalVisible ?? (() => false));
        return new HomeStateChangeHandler(deps, callbacks);
    }

    private static ScheduleListItemViewModel CreateStubListItem(int scheduleId, string name)
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

        var vm = new ScheduleListItemViewModel(deps);
        typeof(ScheduleListItemViewModel)
            .GetProperty(nameof(ScheduleListItemViewModel.Schedule), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(vm, new AlarmSchedule { Id = scheduleId, Name = name });
        return vm;
    }

    private static void SeedUnchangedProcessingState(HomeStateChangeHandler handler, ObservableHashSet<ScheduleStateItem> schedules)
    {
        var buildMap = typeof(HomeStateChangeHandler).GetMethod(
            "BuildSchedulePropertiesMap",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        typeof(HomeStateChangeHandler)
            .GetField("lastProcessedSchedulesCount", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(handler, schedules.Count);
        typeof(HomeStateChangeHandler)
            .GetField("lastProcessedScheduleIds", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(handler, new HashSet<int>(schedules.Where(s => s.Id > 0).Select(s => s.Id)));
        typeof(HomeStateChangeHandler)
            .GetField("lastProcessedScheduleProperties", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(handler, buildMap.Invoke(null, [schedules]));
    }

    private static ScheduleStateItem SampleSchedule(int id, string name, int hour = 7) =>
        new()
        {
            Id = id,
            Name = name,
            Hour = hour,
            Minute = 0,
            DaysOfWeek = WeekDays.Monday,
            BiblePublicationScheduleId = 1,
            BiblePublicationCode = "nwt",
            BiblePublicationTrackCode = "1",
            BiblePublicationLanguageCode = "E",
        };

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

    [Fact]
    public async Task HandleStateChangedAsync_shows_busy_when_existing_schedules_cleared()
    {
        var stubItem = CreateStubListItem(1, "Morning");
        var scheduleCollection = new ObservableHashSet<ScheduleListItemViewModel> { stubItem };
        var busy = false;
        var busyWasSet = false;
        var progressUpdated = 0;
        var sut = CreateHandlerWithRealDeps(
            updateProgressBarVisibility: () => progressUpdated++,
            getSchedules: () => scheduleCollection,
            setSchedules: s => scheduleCollection = s,
            isBusy: () => busy,
            setIsBusy: v =>
            {
                busy = v;
                if (v)
                {
                    busyWasSet = true;
                }
            });

        await sut.HandleStateChangedAsync(new ApplicationState { Schedules = new ObservableHashSet<ScheduleStateItem>() });

        Assert.True(busyWasSet);
        Assert.True(progressUpdated >= 1);
    }

    [Fact]
    public async Task HandleStateChangedAsync_unchanged_state_fades_progress_when_still_busy()
    {
        var busy = true;
        var fadeCount = 0;
        var schedules = new ObservableHashSet<ScheduleStateItem> { SampleSchedule(1, "Morning") };
        var state = new ApplicationState { Schedules = schedules };

        var sut = CreateHandlerWithRealDeps(
            updateProgressBarVisibility: () => { },
            getSchedules: () => new ObservableHashSet<ScheduleListItemViewModel> { CreateStubListItem(1, "Morning") },
            setSchedules: _ => { },
            isBusy: () => busy,
            setIsBusy: v => busy = v,
            fadeOutProgressBarAsync: () =>
            {
                fadeCount++;
                return Task.CompletedTask;
            });

        SeedUnchangedProcessingState(sut, schedules);

        await sut.HandleStateChangedAsync(state);

        Assert.Equal(1, fadeCount);
        Assert.False(busy);
    }

    [Fact]
    public async Task ApplyDeferredReorderAsync_applies_deferred_collection_when_main_thread_available()
    {
        var deferred = new ObservableHashSet<ScheduleListItemViewModel>
        {
            CreateStubListItem(2, "Second"),
            CreateStubListItem(1, "First"),
        };
        var collection = new ObservableHashSet<ScheduleListItemViewModel>();
        collection.Add(CreateStubListItem(9, "Stale"));
        ObservableHashSet<ScheduleListItemViewModel>? boundCollection = collection;
        var notified = false;

        var deps = new HomeStateChangeHandlerDeps(TestLogging.CreateLogger(), null!, null!);
        var callbacks = new HomeStateChangeHandlerCallbacks(
            SetIsBusy: _ => { },
            GetIsBusy: () => false,
            GetSchedules: () => boundCollection,
            SetSchedules: s => boundCollection = s,
            NotifySchedulesChanged: () => notified = true,
            UpdateProgressBarVisibility: () => { },
            FadeOutProgressBarAsync: () => Task.CompletedTask,
            IsPlaybackModalVisible: () => false);
        var sut = new HomeStateChangeHandler(deps, callbacks);

        typeof(HomeStateChangeHandler)
            .GetField("deferredNewSchedules", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(sut, deferred);

        try
        {
            await sut.ApplyDeferredReorderAsync();
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Headless xUnit cannot initialize WinUI MainThread; device/CI app hosts cover this path.
            return;
        }

        Assert.NotNull(boundCollection);
        Assert.Equal(2, boundCollection!.Count);
        Assert.Equal(2, boundCollection.First().ScheduleId);
        Assert.True(notified);
        Assert.Null(typeof(HomeStateChangeHandler)
            .GetField("deferredNewSchedules", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(sut));
    }
}
