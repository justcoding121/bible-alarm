#nullable enable

using System.Linq;
using System.Reflection;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.UI;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.Shared.Models.Media;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class AppLifecycleServiceTests
{
    private sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
            Dispatched.Add(action);
            ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(action));
        }
    }

    private sealed class StubPlaybackModal : IPlaybackModalService
    {
        public int MiniBarCalls { get; private set; }

        public bool IsMinimized => false;

        public bool IsModalOpenOrPending => false;

        public void Dispose()
        {
        }

        public void SubscribeToPlaybackStateChanges()
        {
        }

        public Task<bool> ShowPlaybackModalIfNeededOnWindowCreationAsync() => Task.FromResult(false);

        public Task ShowPlaybackModalIfNeededOnResumeAsync() => Task.CompletedTask;

        public void ShowMiniBarIfPlaybackActiveOnResume() =>
            MiniBarCalls++;

        public void UnsubscribeToPlaybackStateChanges()
        {
        }

        public bool WasRecentlyMinimized() => false;
    }

    private sealed class MutablePlaybackState(PlaybackState initial) : IState<PlaybackState>
    {
        public PlaybackState Value { get; set; } = initial;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class StubAudioPlayer : IAudioPlayer
    {
        public PlayStatus Status { get; init; }
        public bool IsActuallyPlayingOrPaused { get; init; }

        public TimeSpan? CurrentPosition => null;

        public TimeSpan Duration => TimeSpan.Zero;

        public void Dispose()
        {
        }

#pragma warning disable CS0067
        public event EventHandler<EventArgs>? MediaEnded;

        public event EventHandler<EventArgs>? MediaFailed;
#pragma warning restore CS0067

        public Task PrepareAsync(AudioPlayerTrack track) => Task.CompletedTask;
        public Task PlayAsync() => Task.CompletedTask;
        public Task PauseAsync() => Task.CompletedTask;
        public Task ResumeAsync() => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;
        public Task ResetAsync() => Task.CompletedTask;
        public Task SeekToAsync(TimeSpan position) => Task.CompletedTask;
        public Task SetMutedAsync(bool muted) => Task.CompletedTask;
        public void NotifyTrackTransitionStarting()
        {
        }

        public Task SyncMetadataForTrackAsync(AudioPlayerTrack track) => Task.CompletedTask;
    }

    private sealed class DictionaryServiceProvider(params (Type Key, object? Obj)[] entries) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            foreach (var (key, obj) in entries)
            {
                if (key == serviceType)
                {
                    return obj;
                }
            }

            return null;
        }
    }

    private sealed class IdleReviewPrompt : IReviewPromptService
    {
        public Task RecordAppOpenAsync() => Task.CompletedTask;

        public Task RecordDismissEngagementAndRequestIfEligibleAsync() => Task.CompletedTask;
    }

    private sealed class ThrowingReviewPrompt : IReviewPromptService
    {
        public Task RecordAppOpenAsync() => Task.FromException(new InvalidOperationException("boom"));

        public Task RecordDismissEngagementAndRequestIfEligibleAsync() =>
            Task.FromException(new InvalidOperationException("boom"));
    }

    private static PlaybackState Playback(
        bool isPreparingOrPlaying,
        PlayStatus status) =>
        new(
            new PlaybackTransportSlice(1, isPreparingOrPlaying, false, false, status, false, false),
            new PlaybackMediaSlice(null, null, null, null, TimeSpan.Zero, null),
            new PlaybackDefaultScheduleSlice(null, null, null, null, null));

    private static AppLifecycleService CreateSut(IServiceProvider? sp = null, IReviewPromptService? review = null) =>
        new(
            TestLogging.CreateLogger(),
            sp ?? new DictionaryServiceProvider(),
            review ?? new IdleReviewPrompt());

    private static void ReconcileViaReflection(AppLifecycleService sut)
    {
        var m = typeof(AppLifecycleService).GetMethod(
            "ReconcilePlaybackState",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(m);
        m!.Invoke(sut, null);
    }

    [Fact]
    public void OnSleep_sets_IsInForeground_false()
    {
        App.IsInForeground = true;

        AppLifecycleService.OnSleep();

        Assert.False(App.IsInForeground);

        App.IsInForeground = true;
    }

    [Fact]
    public void OnStart_sets_App_IsInForeground_true()
    {
        App.IsInForeground = false;
        var sut = CreateSut();

        sut.OnStart();

        Assert.True(App.IsInForeground);
        sut.Dispose();
        App.IsInForeground = true;
    }

    [Fact]
    public void OnResume_ShowMiniBarIfPlaybackActiveOnResume_invoked_when_registered()
    {
        var playback = new StubPlaybackModal();
        var sp = new DictionaryServiceProvider(
            (typeof(IPlaybackModalService), playback));

        try
        {
            App.IsInForeground = false;
            var sut = CreateSut(sp);
            sut.OnResume();

            Assert.Equal(1, playback.MiniBarCalls);
            sut.Dispose();
        }
        finally
        {
            App.IsInForeground = true;
        }
    }

    [Fact]
    public void Dispose_without_OnStart_does_not_throw()
    {
        using (CreateSut())
        {
        }
    }

    [Fact]
    public void ReconcilePlaybackState_no_op_when_not_preparing_or_playing()
    {
        var dispatcher = new RecordingDispatcher();
        var playback = new MutablePlaybackState(Playback(isPreparingOrPlaying: false, PlayStatus.Playing));
        var sp = new DictionaryServiceProvider(
            (typeof(IState<PlaybackState>), playback),
            (typeof(IDispatcher), dispatcher));

        using var sut = CreateSut(sp);

        ReconcileViaReflection(sut);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public void ReconcilePlaybackState_no_op_when_audio_player_missing()
    {
        var dispatcher = new RecordingDispatcher();
        var playback = new MutablePlaybackState(Playback(isPreparingOrPlaying: true, PlayStatus.Playing));

        var sp = new DictionaryServiceProvider(
            (typeof(IState<PlaybackState>), playback),
            (typeof(IDispatcher), dispatcher));

        using var sut = CreateSut(sp);

        ReconcileViaReflection(sut);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public void ReconcilePlaybackState_no_op_when_hardware_reports_play_or_pause()
    {
        var dispatcher = new RecordingDispatcher();
        var playback = new MutablePlaybackState(Playback(isPreparingOrPlaying: true, PlayStatus.Stopped));
        var player = new StubAudioPlayer { IsActuallyPlayingOrPaused = true };

        using var sut = CreateSut(new DictionaryServiceProvider(
            (typeof(IState<PlaybackState>), playback),
            (typeof(IAudioPlayer), player),
            (typeof(IDispatcher), dispatcher)));

        ReconcileViaReflection(sut);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public void ReconcilePlaybackState_no_op_while_loading()
    {
        var dispatcher = new RecordingDispatcher();
        var playback = new MutablePlaybackState(Playback(isPreparingOrPlaying: true, PlayStatus.Stopped));
        var player = new StubAudioPlayer
        {
            IsActuallyPlayingOrPaused = false,
            Status = PlayStatus.Loading,
        };

        using var sut = CreateSut(new DictionaryServiceProvider(
            (typeof(IState<PlaybackState>), playback),
            (typeof(IAudioPlayer), player),
            (typeof(IDispatcher), dispatcher)));

        ReconcileViaReflection(sut);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public void ReconcilePlaybackState_dispatches_stopped_when_fluxor_thinks_playing_but_player_idle()
    {
        var dispatcher = new RecordingDispatcher();
        var playback = new MutablePlaybackState(Playback(isPreparingOrPlaying: true, PlayStatus.Stopped));
        var player = new StubAudioPlayer
        {
            IsActuallyPlayingOrPaused = false,
            Status = PlayStatus.Stopped,
        };

        using var sut = CreateSut(new DictionaryServiceProvider(
            (typeof(IState<PlaybackState>), playback),
            (typeof(IAudioPlayer), player),
            (typeof(IDispatcher), dispatcher)));

        ReconcileViaReflection(sut);

#if ANDROID || IOS
        Assert.Collection(
            dispatcher.Dispatched,
            a => Assert.IsType<PlaybackStoppedAction>(a),
            a => Assert.IsType<SetCarPlayScreenAction>(a));
#else
        Assert.IsType<PlaybackStoppedAction>(Assert.Single(dispatcher.Dispatched));
#endif
    }

    [Fact]
    public async Task OnStart_RecordAppOpenAsync_failure_is_swallowed()
    {
        App.IsInForeground = true;
        var sut = CreateSut(review: new ThrowingReviewPrompt());

        sut.OnStart();
        await Task.Delay(150);

        sut.Dispose();
    }
}
