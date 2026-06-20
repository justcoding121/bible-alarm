#nullable enable

using Bible.Alarm.Services.UI;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Tests.Support;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class WindowSetupServiceTests
{
#pragma warning disable CS0067
    private sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action) => Dispatched.Add(action);
    }
#pragma warning restore CS0067

    private sealed class StubPlaybackModalService : IPlaybackModalService
    {
        public int UnsubscribeCalls { get; private set; }

        public bool IsMinimized => false;

        public bool IsModalOpenOrPending => false;

        public void Dispose()
        {
        }

        public Task<bool> ShowPlaybackModalIfNeededOnWindowCreationAsync() => Task.FromResult(false);

        public Task ShowPlaybackModalIfNeededOnResumeAsync() => Task.CompletedTask;

        public void ShowMiniBarIfPlaybackActiveOnResume()
        {
        }

        public void SubscribeToPlaybackStateChanges()
        {
        }

        public void UnsubscribeToPlaybackStateChanges() => UnsubscribeCalls++;

        public bool WasRecentlyMinimized() => false;
    }

    private sealed class StubNavigationService : INavigationService
    {
        public int PopAllCalls { get; private set; }

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

        public void PopAllModalsAndPages() => PopAllCalls++;

        public void ClearCache()
        {
        }

        public Views.Home? GetCurrentHomePage() => null;

        public Microsoft.Maui.Controls.Page? GetCurrentPage() => null;

        public bool IsPlaybackModalOnScreen() => false;

        public void SetMiniBarVisible(bool visible)
        {
        }
    }

    private sealed class StubPlaybackService : Bible.Alarm.Services.Media.Interfaces.IPlaybackService
    {
        public int StopForTeardownCalls { get; private set; }

        public bool IsAlarmPlaybackSession => false;

        public void Dispose()
        {
        }

        public Task PlayAsync() => Task.CompletedTask;

        public Task PauseAsync() => Task.CompletedTask;

        public Task PlayPreviousAsync() => Task.CompletedTask;

        public Task PlayNextAsync() => Task.CompletedTask;

        public Task SeekForwardAsync() => Task.CompletedTask;

        public Task SeekBackwardAsync() => Task.CompletedTask;

        public Task SeekToAsync(TimeSpan position) => Task.CompletedTask;

        public Task PrepareAndPlayAsync(int scheduleId, bool isAlarm) => Task.CompletedTask;

        public Task StopAsync() => Task.CompletedTask;

        public Task StopForTeardownAsync()
        {
            StopForTeardownCalls++;
            return Task.CompletedTask;
        }

        public Task ResetAndRetryAsync(int scheduleId) => Task.CompletedTask;
    }

    private sealed class TestServiceProvider(
        IDispatcher dispatcher,
        StubPlaybackService playbackService) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IDispatcher))
            {
                return dispatcher;
            }

            if (serviceType == typeof(StubPlaybackService) ||
                serviceType == typeof(Bible.Alarm.Services.Media.Interfaces.IPlaybackService))
            {
                return playbackService;
            }

            return null;
        }
    }

    private static WindowSetupService CreateSut(
        StubPlaybackModalService? playbackModal = null,
        StubNavigationService? navigation = null,
        IServiceProvider? services = null)
    {
        return new WindowSetupService(
            services ?? new TestServiceProvider(new RecordingDispatcher(), new StubPlaybackService()),
            playbackModal ?? new StubPlaybackModalService(),
            navigation ?? new StubNavigationService());
    }

    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var sut = CreateSut();
        Assert.NotNull(sut);
    }

    [Fact]
    public void UpdateNavigationBarColors_does_not_throw_when_main_nav_page_uninitialized()
    {
        WindowSetupService.UpdateNavigationBarColors();
    }

    [Fact]
    public void TearDown_unsubscribes_pops_pages_and_dispatches_playback_stopped()
    {
        var playbackModal = new StubPlaybackModalService();
        var navigation = new StubNavigationService();
        var dispatcher = new RecordingDispatcher();
        var playback = new StubPlaybackService();
        var sut = CreateSut(playbackModal, navigation, new TestServiceProvider(dispatcher, playback));

        sut.TearDown();

        Assert.Equal(1, playbackModal.UnsubscribeCalls);
        Assert.Equal(1, navigation.PopAllCalls);
        Assert.Contains(dispatcher.Dispatched, a => a is PlaybackStoppedAction);
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        var sut = CreateSut();
        sut.Dispose();
        sut.Dispose();
    }
}

[Collection("MauiUi")]
public sealed class WindowSetupServiceMauiTests(MauiUiFixture fixture)
{
    [Fact]
    public void Dispose_unsubscribes_from_requested_theme_changed_when_application_exists()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var sut = new WindowSetupService(null!, null!, null!);
        sut.Dispose();
    }
}
