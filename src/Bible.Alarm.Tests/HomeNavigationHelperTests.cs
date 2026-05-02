#nullable enable

using AutoMapper;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Tests.Support;
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

        public void Dispose()
        {
        }

        public Task NavigateToHomeAsync(bool animated = true) => Task.CompletedTask;

        public Task NavigateToScheduleAsync() => Task.CompletedTask;

        public Task NavigateToScheduleAsync(int scheduleId, bool isEnabled) => Task.CompletedTask;

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
        public void Dispatch(object action)
        {
        }

#pragma warning disable CS0067
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067
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
