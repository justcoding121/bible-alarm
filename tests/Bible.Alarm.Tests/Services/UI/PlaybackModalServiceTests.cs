#nullable enable

using System.Reflection;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.UI;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.Views;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Microsoft.Maui.Controls;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class PlaybackModalServiceTests
{
    private static readonly BindingFlags InstanceNonPublic =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private static void SetPlaybackModalField<T>(PlaybackModalService sut, string fieldName, T value)
    {
        var f = typeof(PlaybackModalService).GetField(fieldName, InstanceNonPublic);
        Assert.NotNull(f);
        f!.SetValue(sut, value!);
    }

    private static T GetPlaybackModalField<T>(PlaybackModalService sut, string fieldName)
    {
        var f = typeof(PlaybackModalService).GetField(fieldName, InstanceNonPublic);
        Assert.NotNull(f);
        return (T)f!.GetValue(sut)!;
    }

#pragma warning disable CS0067
    private sealed class NopDispatcher : IDispatcher
    {
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
        }
    }
#pragma warning restore CS0067

    private sealed class ObservablePlaybackState : IState<PlaybackState>
    {
        public PlaybackState Value { get; set; } = new();

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067

        public void NotifyChange() =>
            StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class RecordingPlaybackNavigation : INavigationService
    {
        public int OpenPlaybackModalCount { get; private set; }
        public int PopPlaybackPageCount { get; private set; }
        public List<bool> MiniBarVisibleCalls { get; } = [];

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

        public Task OpenPlaybackModalAsync(bool animated = false)
        {
            OpenPlaybackModalCount++;
            return Task.CompletedTask;
        }

        public Task OpenBatteryOptimizationModalAsync(object bindingContext) => Task.CompletedTask;

        public Task OpenNotificationPermissionModalAsync(object bindingContext) => Task.CompletedTask;

        public Task PopModalAsync() => Task.CompletedTask;

        public Task PopAsync() => Task.CompletedTask;

        public Task PopPlaybackPageAsync(bool animated = false)
        {
            PopPlaybackPageCount++;
            return Task.CompletedTask;
        }

        public void PopAllModalsAndPages()
        {
        }

        public void ClearCache()
        {
        }

        public Home? GetCurrentHomePage() => null;

        public Page? GetCurrentPage() => null;

        public bool IsPlaybackModalOnScreen() => PlaybackModalOnScreen;

        public void SetMiniBarVisible(bool visible) =>
            MiniBarVisibleCalls.Add(visible);
    }

    private sealed class ConfigurableAudioPlayer : IAudioPlayer
    {
        public PlayStatus Status { get; set; } = PlayStatus.Stopped;

        public bool IsActuallyPlayingOrPaused { get; set; }

        public void Dispose()
        {
        }

        public Task PrepareAsync(Shared.Models.Media.AudioPlayerTrack track) => Task.CompletedTask;

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

        public Task SyncMetadataForTrackAsync(Shared.Models.Media.AudioPlayerTrack track) => Task.CompletedTask;

        public TimeSpan? CurrentPosition => null;

        public TimeSpan Duration => TimeSpan.Zero;

#pragma warning disable CS0067
        public event EventHandler<EventArgs>? MediaEnded;
        public event EventHandler<EventArgs>? MediaFailed;
#pragma warning restore CS0067
    }

    private sealed class BeginStoppingMessageSink : IDisposable
    {
        public int Count { get; private set; }

        public BeginStoppingMessageSink()
        {
            WeakReferenceMessenger.Default.Register<BeginStoppingPlaybackMessage>(this,
                (_, _) => Count++);
        }

        public void Dispose() =>
            WeakReferenceMessenger.Default.Unregister<BeginStoppingPlaybackMessage>(this);
    }

    private sealed class PlaybackModalOpenedSink : IDisposable
    {
        public int Count { get; private set; }

        public PlaybackModalOpenedSink()
        {
            WeakReferenceMessenger.Default.Register<PlaybackModalOpenedMessage>(this, (_, _) => Count++);
        }

        public void Dispose() =>
            WeakReferenceMessenger.Default.Unregister<PlaybackModalOpenedMessage>(this);
    }

    private static PlaybackModalService CreateSut(
        InlineMainThreadRunner mainThreadRunner,
        RecordingPlaybackNavigation? nav = null,
        IState<PlaybackState>? state = null,
        ConfigurableAudioPlayer? audio = null,
        IDispatcher? dispatcher = null)
    {
        return new PlaybackModalService(
            TestLogging.CreateLogger(),
            mainThreadRunner,
            nav ?? new RecordingPlaybackNavigation(),
            state ?? new ObservablePlaybackState(),
            audio ?? new ConfigurableAudioPlayer(),
            dispatcher ?? new NopDispatcher());
    }

    private static PlaybackState PlayingState(bool preparingOrPlaying = true, int? scheduleId = 5) =>
        new(
            new PlaybackTransportSlice(
                scheduleId,
                preparingOrPlaying,
                false,
                false,
                PlayStatus.Playing,
                false,
                false),
            new PlaybackMediaSlice(null, null, null, null, TimeSpan.Zero, null),
            new PlaybackDefaultScheduleSlice(null, null, null, null, null));

    private static PlaybackState LoadingState(int? scheduleId = 7) =>
        new(
            new PlaybackTransportSlice(
                scheduleId,
                true,
                false,
                false,
                PlayStatus.Loading,
                false,
                false),
            new PlaybackMediaSlice(null, null, null, null, TimeSpan.Zero, null),
            new PlaybackDefaultScheduleSlice(null, null, null, null, null));

    [Fact]
    public void WasRecentlyMinimized_returns_false_when_not_minimized()
    {
        var runner = new InlineMainThreadRunner();
        var sut = CreateSut(runner);

        Assert.False(sut.WasRecentlyMinimized());
        sut.Dispose();
    }

    [Fact]
    public void IsMinimized_false_by_default()
    {
        var runner = new InlineMainThreadRunner();
        var sut = CreateSut(runner);

        Assert.False(sut.IsMinimized);
        sut.Dispose();
    }

    [Fact]
    public void WasRecentlyMinimized_returns_true_inside_cooldown_after_reflection_prime()
    {
        var runner = new InlineMainThreadRunner();
        var sut = CreateSut(runner);

        SetPlaybackModalField(sut, "isMinimized", true);
        SetPlaybackModalField(sut, "lastMinimizedAtUtc", DateTime.UtcNow);

        Assert.True(sut.WasRecentlyMinimized());
        sut.Dispose();
    }

    [Fact]
    public void WasRecentlyMinimized_returns_false_when_cooldown_elapsed()
    {
        var runner = new InlineMainThreadRunner();
        var sut = CreateSut(runner);

        SetPlaybackModalField(sut, "isMinimized", true);
        SetPlaybackModalField(sut, "lastMinimizedAtUtc", DateTime.UtcNow.AddMilliseconds(-600));

        Assert.False(sut.WasRecentlyMinimized());
        sut.Dispose();
    }

    [Fact]
    public void Receive_RequestShow_modal_open_same_schedule_clears_pending_flags()
    {
        var runner = new InlineMainThreadRunner();
        var playback = new ObservablePlaybackState
        {
            Value = PlayingState(scheduleId: 9),
        };

        PlaybackModalService sut = null!;
        try
        {
            sut = CreateSut(runner, state: playback);
            SetPlaybackModalField(sut, "isModalOpen", true);

            sut.Receive(new RequestShowPlaybackModalMessage { TargetScheduleId = 9 });

            Assert.False(GetPlaybackModalField<bool>(sut, "requestedShowModal"));
            Assert.Null(GetPlaybackModalField<int?>(sut, "targetScheduleId"));
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void Receive_RequestShow_modal_open_different_schedule_keeps_pending_flags()
    {
        var runner = new InlineMainThreadRunner();
        var playback = new ObservablePlaybackState
        {
            Value = PlayingState(scheduleId: 10),
        };

        PlaybackModalService sut = null!;
        try
        {
            sut = CreateSut(runner, state: playback);
            SetPlaybackModalField(sut, "isModalOpen", true);

            sut.Receive(new RequestShowPlaybackModalMessage { TargetScheduleId = 99 });

            Assert.True(GetPlaybackModalField<bool>(sut, "requestedShowModal"));
            Assert.Equal(99, GetPlaybackModalField<int?>(sut, "targetScheduleId"));
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void Receive_RequestShow_minimized_other_schedule_stops_via_messenger_before_maximize_same_schedule_avoids_it()
    {
        var runnerA = new InlineMainThreadRunner();
        var runnerB = new InlineMainThreadRunner();
        var playback = new ObservablePlaybackState
        {
            Value = PlayingState(scheduleId: 3),
        };

        using var sinkDifferent = new BeginStoppingMessageSink();
        PlaybackModalService sut = null!;
        try
        {
            sut = CreateSut(runnerA, state: playback);
            SetPlaybackModalField(sut, "isMinimized", true);

            sut.Receive(new RequestShowPlaybackModalMessage { TargetScheduleId = 4 });

            Assert.Equal(1, sinkDifferent.Count);
        }
        finally
        {
            sut?.Dispose();
        }

        using var sinkSame = new BeginStoppingMessageSink();
        PlaybackModalService sut2 = null!;
        try
        {
            sut2 = CreateSut(runnerB, state: playback);
            SetPlaybackModalField(sut2, "isMinimized", true);

            sut2.Receive(new RequestShowPlaybackModalMessage { TargetScheduleId = 3 });

            Assert.Equal(0, sinkSame.Count);
        }
        finally
        {
            sut2?.Dispose();
        }
    }

    [Fact]
    public async Task Receive_ExplicitStop_when_modal_closed_sets_bypass_without_popping_navigation()
    {
        var runner = new InlineMainThreadRunner();
        var nav = new RecordingPlaybackNavigation();
        PlaybackModalService sut = null!;
        try
        {
            sut = CreateSut(runner, nav);
            sut.Receive(new PlaybackExplicitStopMessage());

            Assert.True(GetPlaybackModalField<bool>(sut, "bypassPopGenerationGuard"));
            Assert.False(GetPlaybackModalField<bool>(sut, "requestedShowModal"));
            await runner.DrainAsync();
            Assert.Equal(0, nav.PopPlaybackPageCount);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public async Task Receive_ExplicitStop_when_modal_open_pops_and_resets_mini_and_pending_flags()
    {
        var runner = new InlineMainThreadRunner();
        var nav = new RecordingPlaybackNavigation { PlaybackModalOnScreen = true };
        PlaybackModalService sut = null!;
        try
        {
            sut = CreateSut(runner, nav);
            SetPlaybackModalField(sut, "isModalOpen", true);
            SetPlaybackModalField(sut, "requestedShowModal", true);
            SetPlaybackModalField(sut, "targetScheduleId", 123);

            sut.Receive(new PlaybackExplicitStopMessage());
            await runner.DrainAsync();

            Assert.Equal(1, nav.PopPlaybackPageCount);
            Assert.Contains(false, nav.MiniBarVisibleCalls);
            Assert.False(GetPlaybackModalField<bool>(sut, "isModalOpen"));
            Assert.False(GetPlaybackModalField<bool>(sut, "requestedShowModal"));
            Assert.False(GetPlaybackModalField<bool>(sut, "isMinimized"));
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public async Task ShowPlaybackModalIfNeededOnResumeAsync_returns_when_modal_already_on_navigation_stack()
    {
        var runner = new InlineMainThreadRunner();
        var nav = new RecordingPlaybackNavigation { PlaybackModalOnScreen = true };
        var playback = new ObservablePlaybackState { Value = PlayingState() };

        PlaybackModalService sut = null!;
        try
        {
            sut = CreateSut(runner, nav, playback);
            SetPlaybackModalField(sut, "isModalOpen", true);

            await sut.ShowPlaybackModalIfNeededOnResumeAsync();

            Assert.Equal(0, nav.OpenPlaybackModalCount);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public async Task ShowPlaybackModalIfNeededOnWindowCreationAsync_true_when_already_flagged_modal_open_without_delay_branch()
    {
        var runner = new InlineMainThreadRunner();
        var playback = new ObservablePlaybackState { Value = PlayingState() };
        var nav = new RecordingPlaybackNavigation();

        PlaybackModalService sut = null!;
        try
        {
            sut = CreateSut(runner, nav, playback);
            SetPlaybackModalField(sut, "isModalOpen", true);

            var shown = await sut.ShowPlaybackModalIfNeededOnWindowCreationAsync();

            Assert.True(shown);
            Assert.Equal(0, nav.OpenPlaybackModalCount);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public async Task Subscribe_OnPlayback_skips_processing_when_PlaybackState_unreadable()
    {
        var runner = new InlineMainThreadRunner();
        var unreadable = new UnreadablePlaybackState();
        PlaybackModalService sut = null!;
        try
        {
            sut = CreateSut(runner, state: unreadable);
            sut.SubscribeToPlaybackStateChanges();
            unreadable.NotifyChange();
            await runner.DrainAsync();
            Assert.False(sut.IsMinimized);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public async Task OnPlayback_minimized_loading_with_pending_show_matches_schedule_queues_maximize()
    {
        var runner = new InlineMainThreadRunner();
        var nav = new RecordingPlaybackNavigation();
        nav.PlaybackModalOnScreen = true;
        var playback = new ObservablePlaybackState { Value = LoadingState(15) };
        var audio = new ConfigurableAudioPlayer
        {
            Status = PlayStatus.Loading,
            IsActuallyPlayingOrPaused = false,
        };

        PlaybackModalService sut = null!;
        try
        {
            sut = CreateSut(runner, nav, playback, audio);

            sut.SubscribeToPlaybackStateChanges();

            SetPlaybackModalField(sut, "isMinimized", true);
            SetPlaybackModalField(sut, "requestedShowModal", true);
            SetPlaybackModalField(sut, "targetScheduleId", 15);

            playback.NotifyChange();
            await runner.DrainAsync();

            Assert.Equal(1, nav.OpenPlaybackModalCount);
            Assert.False(GetPlaybackModalField<bool>(sut, "requestedShowModal"));
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public async Task OnPlayback_preserves_requested_show_modal_during_transient_inactive_without_navigation()
    {
        var runner = new InlineMainThreadRunner();
        var nav = new RecordingPlaybackNavigation();
        var playback = new ObservablePlaybackState
        {
            Value = new PlaybackState(),
        };

        PlaybackModalService sut = null!;
        try
        {
            sut = CreateSut(runner, nav, playback);
            sut.SubscribeToPlaybackStateChanges();

            SetPlaybackModalField(sut, "requestedShowModal", true);
            playback.NotifyChange();
            await runner.DrainAsync();

            Assert.True(GetPlaybackModalField<bool>(sut, "requestedShowModal"));
            Assert.Equal(0, nav.OpenPlaybackModalCount);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public async Task OnPlayback_active_without_ui_queues_mini_bar_via_main_thread_delegate()
    {
        var runner = new InlineMainThreadRunner();
        var nav = new RecordingPlaybackNavigation();
        using var sink = new PlaybackModalOpenedSink();
        var playback = new ObservablePlaybackState { Value = PlayingState() };

        PlaybackModalService sut = null!;
        try
        {
            sut = CreateSut(runner, nav, playback);
            sut.SubscribeToPlaybackStateChanges();

            playback.NotifyChange();
            await runner.DrainAsync();

            Assert.Contains(true, nav.MiniBarVisibleCalls);
            Assert.True(GetPlaybackModalField<bool>(sut, "isMinimized"));
            Assert.Equal(1, sink.Count);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public async Task OnPlayback_modal_visible_playing_and_pending_show_clears_request_and_shows_generation_change()
    {
        var runner = new InlineMainThreadRunner();
        var playback = new ObservablePlaybackState { Value = PlayingState() };
        var nav = new RecordingPlaybackNavigation();

        PlaybackModalService sut = null!;
        try
        {
            sut = CreateSut(runner, nav, playback);
            sut.SubscribeToPlaybackStateChanges();

            SetPlaybackModalField(sut, "isModalOpen", true);
            SetPlaybackModalField(sut, "requestedShowModal", true);

            playback.NotifyChange();
            await runner.DrainAsync();

            Assert.Equal(1, GetPlaybackModalField<int>(sut, "popGeneration"));
            Assert.False(GetPlaybackModalField<bool>(sut, "requestedShowModal"));
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public async Task MaximizeAsync_zombie_when_player_inactive_without_fluxor_preparing_gate_clears_playback_ui_flags()
    {
        var runner = new InlineMainThreadRunner();
        var nav = new RecordingPlaybackNavigation();
        var playback = new ObservablePlaybackState
        {
            Value = new PlaybackState(),
        };
        var audio = new ConfigurableAudioPlayer();

        PlaybackModalService sut = null!;
        try
        {
            sut = CreateSut(runner, nav, playback, audio);
            SetPlaybackModalField(sut, "isMinimized", true);

            audio.Status = PlayStatus.Stopped;
            audio.IsActuallyPlayingOrPaused = false;

            sut.Receive(new MaximizePlaybackMessage());
            await runner.DrainAsync();

            Assert.Equal(0, nav.OpenPlaybackModalCount);
            Assert.False(GetPlaybackModalField<bool>(sut, "isModalOpen"));
            Assert.False(GetPlaybackModalField<bool>(sut, "isMinimized"));
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public async Task MaximizeAsync_proceeds_when_player_inactive_but_fluxor_preparing_gate_true()
    {
        var runner = new InlineMainThreadRunner();
        var nav = new RecordingPlaybackNavigation { PlaybackModalOnScreen = true };
        var playback = new ObservablePlaybackState
        {
            Value = PlayingState(preparingOrPlaying: true),
        };
        var audio = new ConfigurableAudioPlayer();

        PlaybackModalService sut = null!;
        try
        {
            sut = CreateSut(runner, nav, playback, audio);
            SetPlaybackModalField(sut, "isMinimized", true);

            audio.Status = PlayStatus.Stopped;
            audio.IsActuallyPlayingOrPaused = false;

            sut.Receive(new MaximizePlaybackMessage());
            await runner.DrainAsync();

            Assert.Equal(1, nav.OpenPlaybackModalCount);
            Assert.False(GetPlaybackModalField<bool>(sut, "isMinimized"));
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public async Task MinimizeAsync_early_aborts_modal_still_visible_after_retries()
    {
        var runner = new InlineMainThreadRunner();
        var nav = new RecordingPlaybackNavigation { PlaybackModalOnScreen = true };
        var sut = CreateSut(runner, nav);
        PlaybackModalService? local = sut;
        try
        {
            SetPlaybackModalField(local, "isModalOpen", true);

            local.Receive(new MinimizePlaybackMessage());
            await runner.DrainAsync();

            Assert.False(local.IsMinimized);
        }
        finally
        {
            local?.Dispose();
        }
    }

    private sealed class UnreadablePlaybackState : IState<PlaybackState>
    {
        public PlaybackState Value =>
            throw new InvalidOperationException("fluxor unavailable");

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067

        public void NotifyChange() =>
            StateChanged?.Invoke(this, EventArgs.Empty);
    }

    [Fact]
    public void Ctor_accepts_dependencies()
    {
        PlaybackModalService sut = null!;
        try
        {
            sut = new PlaybackModalService(
                TestLogging.CreateLogger(),
                new InlineMainThreadRunner(),
                new RecordingPlaybackNavigation(),
                new ObservablePlaybackState(),
                new ConfigurableAudioPlayer(),
                new NopDispatcher());

            Assert.NotNull(sut);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void IsModalOpenOrPending_reflects_modal_open_and_pending_request_flags()
    {
        var runner = new InlineMainThreadRunner();
        var sut = CreateSut(runner);
        try
        {
            Assert.False(sut.IsModalOpenOrPending);

            SetPlaybackModalField(sut, "requestedShowModal", true);
            Assert.True(sut.IsModalOpenOrPending);

            SetPlaybackModalField(sut, "requestedShowModal", false);
            SetPlaybackModalField(sut, "isModalOpen", true);
            Assert.True(sut.IsModalOpenOrPending);
        }
        finally
        {
            sut.Dispose();
        }
    }

    [Fact]
    public void ShowMiniBarIfPlaybackActiveOnResume_shows_bar_when_playback_is_active()
    {
        var runner = new InlineMainThreadRunner();
        var nav = new RecordingPlaybackNavigation();
        var state = new ObservablePlaybackState { Value = PlayingState() };
        var sut = CreateSut(runner, nav, state);
        try
        {
            sut.ShowMiniBarIfPlaybackActiveOnResume();

            Assert.Contains(true, nav.MiniBarVisibleCalls);
        }
        finally
        {
            sut.Dispose();
        }
    }

    [Fact]
    public void ShowMiniBarIfPlaybackActiveOnResume_shows_bar_when_minimized()
    {
        var runner = new InlineMainThreadRunner();
        var nav = new RecordingPlaybackNavigation();
        var sut = CreateSut(runner, nav);
        try
        {
            SetPlaybackModalField(sut, "isMinimized", true);

            sut.ShowMiniBarIfPlaybackActiveOnResume();

            Assert.Contains(true, nav.MiniBarVisibleCalls);
        }
        finally
        {
            sut.Dispose();
        }
    }

    [Fact]
    public void UnsubscribeToPlaybackStateChanges_stops_state_change_handler()
    {
        var runner = new InlineMainThreadRunner();
        var nav = new RecordingPlaybackNavigation();
        var state = new ObservablePlaybackState { Value = PlayingState() };
        var sut = CreateSut(runner, nav, state);
        try
        {
            sut.SubscribeToPlaybackStateChanges();
            sut.UnsubscribeToPlaybackStateChanges();

            state.Value = PlayingState(preparingOrPlaying: true, scheduleId: 99);
            state.NotifyChange();

            Assert.Equal(0, nav.OpenPlaybackModalCount);
        }
        finally
        {
            sut.Dispose();
        }
    }
}
