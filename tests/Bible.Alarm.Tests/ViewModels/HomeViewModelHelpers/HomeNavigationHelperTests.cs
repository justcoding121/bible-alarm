#nullable enable

using System.Reflection;
using AutoMapper;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels;
using Bible.Alarm.ViewModels.HomeViewModelHelpers;
using Bible.Alarm.Views;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Maui.Controls;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class HomeNavigationHelperTests
{
    private sealed class FakePlaybackState(PlaybackState value) : IState<PlaybackState>
    {
        public PlaybackState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class StubNavigationService : INavigationService
    {
        public bool PlaybackModalOnScreen { get; set; }
        public int? NavigatedScheduleId { get; private set; }
        public bool? NavigatedIsEnabled { get; private set; }

        public void Dispose()
        {
        }

        public Task NavigateToHomeAsync(bool animated = true) => Task.CompletedTask;

        public Task NavigateToScheduleAsync() => Task.CompletedTask;

        public Task NavigateToScheduleAsync(int scheduleId, bool isEnabled)
        {
            NavigatedScheduleId = scheduleId;
            NavigatedIsEnabled = isEnabled;
            return Task.CompletedTask;
        }

        public Task OpenSongPublicationSelectionModalAsync(object bindingContext) => Task.CompletedTask;

        public Task OpenMusicTrackSelectionModalAsync(object bindingContext) => Task.CompletedTask;

        public Task OpenBibleSelectionModalAsync(object bindingContext) => Task.CompletedTask;

        public Task OpenSectionSelectionModalAsync(object bindingContext) => Task.CompletedTask;

        public Task OpenMusicSectionSelectionModalAsync(object bindingContext) => Task.CompletedTask;

        public Task OpenBiblePublicationTrackSelectionModalAsync(object bindingContext) => Task.CompletedTask;

        public Task OpenNumberOfTracksModalAsync(object bindingContext) => Task.CompletedTask;

        public Task OpenLanguageModalAsync(object bindingContext) => Task.CompletedTask;

        public Task OpenCategoryModalAsync(object bindingContext) => Task.CompletedTask;

        public Task OpenPlaybackModalAsync(bool animated = false) => Task.CompletedTask;

        public Task OpenBatteryOptimizationModalAsync(object bindingContext) => Task.CompletedTask;

        public Task OpenNotificationPermissionModalAsync(object bindingContext) => Task.CompletedTask;

        public Task PopModalAsync() => Task.CompletedTask;

        public Task PopAsync() => Task.CompletedTask;

        public Task PopPlaybackPageAsync(bool animated = false) => Task.CompletedTask;

        public void PopAllModalsAndPages()
        {
        }

        public void ClearCache()
        {
        }

        public Home? GetCurrentHomePage() => null;

        public Page? GetCurrentPage() => null;

        public bool IsPlaybackModalOnScreen() => PlaybackModalOnScreen;

        public void SetMiniBarVisible(bool visible)
        {
        }
    }

    private sealed class StubPlaybackModalService : IPlaybackModalService
    {
        public bool RecentlyMinimizedResult { get; set; }

        public void Dispose()
        {
        }

        public void SubscribeToPlaybackStateChanges()
        {
        }

        public void UnsubscribeToPlaybackStateChanges()
        {
        }

        public Task<bool> ShowPlaybackModalIfNeededOnWindowCreationAsync() => Task.FromResult(false);

        public Task ShowPlaybackModalIfNeededOnResumeAsync() => Task.CompletedTask;

        public void ShowMiniBarIfPlaybackActiveOnResume()
        {
        }

        public bool IsMinimized => false;

        public bool IsModalOpenOrPending => false;

        public bool WasRecentlyMinimized() => RecentlyMinimizedResult;
    }

    private sealed class NopDispatcher : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

        public void Dispatch(object action) => Dispatched.Add(action);

#pragma warning disable CS0067
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067
    }

    private sealed class FakeApplicationState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class NopSchedulePlaybackService : ISchedulePlaybackService
    {
        public Task<bool> CanMoveTrackAsync(int scheduleId) => Task.FromResult(true);

        public Task PlayScheduleAsync(int scheduleId) => Task.CompletedTask;
    }

    private sealed class NopStopPlaybackService : IPlaybackService
    {
        public bool IsAlarmPlaybackSession => false;

        public Task PauseAsync() => Task.CompletedTask;

        public Task PlayAsync() => Task.CompletedTask;

        public Task PlayNextAsync() => Task.CompletedTask;

        public Task PlayPreviousAsync() => Task.CompletedTask;

        public Task PrepareAndPlayAsync(int scheduleId, bool isAlarm) => Task.CompletedTask;

        public Task ResetAndRetryAsync(int scheduleId) => Task.CompletedTask;

        public Task SeekBackwardAsync() => Task.CompletedTask;

        public Task SeekForwardAsync() => Task.CompletedTask;

        public Task SeekToAsync(TimeSpan position) => Task.CompletedTask;

        public Task StopAsync() => Task.CompletedTask;

        public Task StopForTeardownAsync() => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    private sealed class NopScheduleStateService : IScheduleStateService
    {
        public void Dispose()
        {
        }

        public Task<bool> UpdateScheduleEnabledStateAsync(int scheduleId, bool isEnabled) =>
            Task.FromResult(true);
    }

    private sealed class StubCategoryNameService : ICategoryNameService
    {
        public Task WarmCacheForDisplayLanguageAsync(string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public string? GetName(string categoryCode, string displayLanguageCode) => categoryCode;
    }

    private static HomeNavigationHelper CreateSut(
        StubNavigationService nav,
        StubPlaybackModalService playbackModal,
        IState<PlaybackState> playbackState)
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var mapper = new MapperConfiguration(_ => { }, NullLoggerFactory.Instance).CreateMapper();
        return new HomeNavigationHelper(
            TestLogging.CreateLogger(),
            new NopDispatcher(),
            nav,
            playbackModal,
            playbackState,
            services,
            mapper);
    }

    [Fact]
    public void ShouldSkipNavigation_true_when_playback_modal_on_screen_for_same_schedule()
    {
        var nav = new StubNavigationService { PlaybackModalOnScreen = true };
        var modalSvc = new StubPlaybackModalService();
        var state = new FakePlaybackState(new PlaybackState
        {
            IsPreparingOrPlaying = true,
            CurrentScheduleId = 10,
        });
        var sut = CreateSut(nav, modalSvc, state);

        Assert.True(sut.ShouldSkipNavigation(10));
    }

    [Fact]
    public void ShouldSkipNavigation_false_when_modal_off_screen()
    {
        var nav = new StubNavigationService { PlaybackModalOnScreen = false };
        var modalSvc = new StubPlaybackModalService();
        var state = new FakePlaybackState(new PlaybackState
        {
            IsPreparingOrPlaying = true,
            CurrentScheduleId = 10,
        });
        var sut = CreateSut(nav, modalSvc, state);

        Assert.False(sut.ShouldSkipNavigation(10));
    }

    [Fact]
    public void ShouldSkipNavigation_true_when_recently_minimized_guard_trips()
    {
        var nav = new StubNavigationService();
        var modalSvc = new StubPlaybackModalService { RecentlyMinimizedResult = true };
        var state = new FakePlaybackState(new PlaybackState());
        var sut = CreateSut(nav, modalSvc, state);

        Assert.True(sut.ShouldSkipNavigation(4));
    }

    [Fact]
    public void ShouldSkipNavigation_true_shortly_after_track_play_click()
    {
        var nav = new StubNavigationService();
        var modalSvc = new StubPlaybackModalService();
        var state = new FakePlaybackState(new PlaybackState());
        var sut = CreateSut(nav, modalSvc, state);

        sut.TrackPlayClick(7);
        Assert.True(sut.ShouldSkipNavigation(7));
    }

    [Fact]
    public async Task ShowOverlayAndNavigateAsync_returns_when_schedule_missing()
    {
        var nav = new StubNavigationService();
        var dispatcher = new NopDispatcher();
        var sut = new HomeNavigationHelper(
            TestLogging.CreateLogger(),
            dispatcher,
            nav,
            new StubPlaybackModalService(),
            new FakePlaybackState(new PlaybackState()),
            new ServiceCollection().BuildServiceProvider(),
            new MapperConfiguration(_ => { }, NullLoggerFactory.Instance).CreateMapper());

        using var missing = CreateListItem(schedule: null);
        await sut.ShowOverlayAndNavigateAsync(missing);

        Assert.Empty(dispatcher.Dispatched);
        Assert.Null(nav.NavigatedScheduleId);
    }

    [Fact]
    public async Task ShowOverlayAndNavigateAsync_dispatches_overlay_and_navigates()
    {
        var nav = new StubNavigationService();
        var dispatcher = new NopDispatcher();
        var sut = new HomeNavigationHelper(
            TestLogging.CreateLogger(),
            dispatcher,
            nav,
            new StubPlaybackModalService(),
            new FakePlaybackState(new PlaybackState()),
            new ServiceCollection().BuildServiceProvider(),
            new MapperConfiguration(_ => { }, NullLoggerFactory.Instance).CreateMapper());
        using var listItem = CreateListItem(new AlarmSchedule { Id = 42, Name = "Dawn", IsEnabled = true });

        await sut.ShowOverlayAndNavigateAsync(listItem);

        var overlay = Assert.Single(dispatcher.Dispatched.OfType<SetSchedulePageOverlayAction>());
        Assert.True(overlay.IsVisible);
        Assert.Equal(42, nav.NavigatedScheduleId);
        Assert.Equal(listItem.IsEnabled, nav.NavigatedIsEnabled);
    }

    private static ScheduleListItemViewModel CreateListItem(AlarmSchedule? schedule)
    {
        var deps = new ScheduleListItemViewModelDeps(
            TestLogging.CreateLogger(),
            new NopSchedulePlaybackService(),
            new NopStopPlaybackService(),
            new NopScheduleStateService(),
            new FakeApplicationState(new ApplicationState()),
            new FakePlaybackState(new PlaybackState()),
            new NopDispatcher(),
            new MapperConfiguration(c => c.AddProfile<ScheduleMappingProfile>(), NullLoggerFactory.Instance).CreateMapper(),
            new StubCategoryNameService());
        var vm = new ScheduleListItemViewModel(deps);
        if (schedule != null)
        {
            typeof(ScheduleListItemViewModel)
                .GetField("<Schedule>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(vm, schedule);
        }

        return vm;
    }

    [Fact]
    public async Task ShouldSkipNavigation_false_after_play_click_cooldown()
    {
        var nav = new StubNavigationService();
        var modalSvc = new StubPlaybackModalService();
        var state = new FakePlaybackState(new PlaybackState());
        var sut = CreateSut(nav, modalSvc, state);

        sut.TrackPlayClick(7);
        await Task.Delay(550);
        Assert.False(sut.ShouldSkipNavigation(7));
    }
}
