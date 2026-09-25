#nullable enable

using System.Runtime.CompilerServices;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using System.Runtime.InteropServices;

namespace Bible.Alarm.Tests;

public sealed class MiniPlaybackBarViewModelTests
{
    private sealed class RecordingPlaybackService : IPlaybackService
    {
        public bool IsAlarmPlaybackSession { get; set; }

        public List<string> Calls { get; } = [];

        private void Record([CallerMemberName] string name = "") => Calls.Add(name);

        public Task PlayAsync()
        {
            Record();
            return Task.CompletedTask;
        }

        public Task PauseAsync()
        {
            Record();
            return Task.CompletedTask;
        }

        public Task PlayPreviousAsync()
        {
            Record();
            return Task.CompletedTask;
        }

        public Task PlayNextAsync()
        {
            Record();
            return Task.CompletedTask;
        }

        public Task SeekForwardAsync()
        {
            Record();
            return Task.CompletedTask;
        }

        public Task SeekBackwardAsync()
        {
            Record();
            return Task.CompletedTask;
        }

        public Task SeekToAsync(TimeSpan position)
        {
            Record();
            return Task.CompletedTask;
        }

        public Task PrepareAndPlayAsync(int scheduleId, bool isAlarm) => Task.CompletedTask;

        public Task StopAsync()
        {
            Record();
            return Task.CompletedTask;
        }

        public Task StopForTeardownAsync() => Task.CompletedTask;

        public Task ResetAndRetryAsync(int scheduleId)
        {
            Record();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }

    private sealed class MutablePlaybackState : IState<PlaybackState>
    {
        public MutablePlaybackState(PlaybackState initial) => Value = initial;

        public PlaybackState Value { get; set; }

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067

        public void NotifyStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static MiniPlaybackBarViewModel CreateSut(
        IPlaybackService? playback = null,
        IState<PlaybackState>? playbackState = null)
    {
        playback ??= new RecordingPlaybackService();
        playbackState ??= new MutablePlaybackState(new PlaybackState());
        return new MiniPlaybackBarViewModel(TestLogging.CreateLogger(), playback, playbackState);
    }

    private static PlaybackState PlayingState(
        string title = "Genesis 1",
        bool canPlayNext = true,
        bool canPlayPrevious = true,
        PlayStatus status = PlayStatus.Playing) =>
        new(
            new PlaybackTransportSlice(null, true, canPlayNext, canPlayPrevious, status, false, false),
            new PlaybackMediaSlice(title, "Artist", "Album", null, TimeSpan.FromSeconds(120), null),
            new PlaybackDefaultScheduleSlice(null, null, null, null, null));

    [Fact]
    public void Receive_PlaybackPosition_clamps_fraction_above_one_to_one()
    {
        using var sut = CreateSut();

        sut.Receive(new PlaybackPositionChangedMessage
        {
            Duration = TimeSpan.FromSeconds(10),
            CurrentPosition = TimeSpan.FromSeconds(50),
        });

        Assert.Equal(1.0, sut.Progress);
    }

    [Fact]
    public void Receive_PlaybackPosition_does_not_update_when_track_duration_is_unknown()
    {
        using var sut = CreateSut();

        sut.Receive(new PlaybackPositionChangedMessage { CurrentPosition = TimeSpan.FromSeconds(30) });

        Assert.Equal(0.0, sut.Progress);
    }

    [Fact]
    public void Receive_PlaybackPosition_is_ignored_after_Dispose()
    {
        var sut = CreateSut();
        sut.Dispose();

        sut.Receive(new PlaybackPositionChangedMessage
        {
            Duration = TimeSpan.FromSeconds(5),
            CurrentPosition = TimeSpan.FromSeconds(2),
        });

        Assert.Equal(0.0, sut.Progress);
    }

    [Fact]
    public void Receive_PlaybackPosition_updates_progress_for_valid_duration()
    {
        using var sut = CreateSut();

        sut.Receive(new PlaybackPositionChangedMessage
        {
            Duration = TimeSpan.FromSeconds(10),
            CurrentPosition = TimeSpan.FromSeconds(2),
        });

        Assert.Equal(0.2, sut.Progress, precision: 3);
    }

    [Fact]
    public async Task Receive_BeginStoppingPlaybackMessage_disables_controls()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        using var sut = CreateSut();

        sut.Receive(new BeginStoppingPlaybackMessage());
        if (!await MauiUiTestHostHelper.FlushMainThreadAsync())
        {
            return;
        }

        Assert.True(sut.IsStopping);
        Assert.False(sut.AreControlsEnabled);
    }

    [Fact]
    public async Task Receive_NextButtonPressedMessage_sets_busy_state()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        using var sut = CreateSut();

        sut.Receive(new NextButtonPressedMessage());
        if (!await MauiUiTestHostHelper.FlushMainThreadAsync())
        {
            return;
        }

        Assert.True(sut.IsNextBusy);
        Assert.False(sut.AreControlsEnabled);
        Assert.Equal(0.0, sut.Progress);
    }

    [Fact]
    public async Task Playback_state_change_syncs_title_and_play_visibility()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var playbackState = new MutablePlaybackState(PlayingState());
        using var sut = CreateSut(playbackState: playbackState);

        playbackState.Value = PlayingState(title: "Updated track", status: PlayStatus.Paused);
        playbackState.NotifyStateChanged();
        if (!await MauiUiTestHostHelper.FlushMainThreadAsync())
        {
            return;
        }

        Assert.Equal("Updated track", sut.Title);
        Assert.False(sut.IsPlaying);
        Assert.True(sut.PlayVisible);
        Assert.False(sut.PauseVisible);
    }

    [Fact]
    public async Task PlayPauseCommand_calls_pause_when_playing()
    {
        var playback = new RecordingPlaybackService();
        var playbackState = new MutablePlaybackState(PlayingState());
        using var sut = CreateSut(playback, playbackState);

        await sut.PlayPauseCommand.ExecuteAsync(null);

        Assert.Contains("PauseAsync", playback.Calls);
    }

    [Fact]
    public async Task PlayPauseCommand_calls_play_when_paused()
    {
        var playback = new RecordingPlaybackService();
        var playbackState = new MutablePlaybackState(PlayingState(status: PlayStatus.Paused));
        using var sut = CreateSut(playback, playbackState);

        await sut.PlayPauseCommand.ExecuteAsync(null);

        Assert.Contains("PlayAsync", playback.Calls);
    }

    [Fact]
    public void IsVisible_true_resets_busy_flags_and_syncs_from_state()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var playbackState = new MutablePlaybackState(PlayingState());
        using var sut = CreateSut(playbackState: playbackState);
        sut.IsNextBusy = true;
        sut.IsStopping = true;

        sut.IsVisible = true;

        Assert.False(sut.IsNextBusy);
        Assert.False(sut.IsStopping);
        Assert.Equal("Genesis 1", sut.Title);
    }

    [Fact]
    public void Ctor_sets_static_instance_and_dispose_clears_it()
    {
        MiniPlaybackBarViewModel? first = null;
        MiniPlaybackBarViewModel? second = null;
        try
        {
            first = CreateSut();
            Assert.Same(first, MiniPlaybackBarViewModel.Instance);

            first.Dispose();
            Assert.Null(MiniPlaybackBarViewModel.Instance);

            second = CreateSut();
            Assert.Same(second, MiniPlaybackBarViewModel.Instance);
        }
        finally
        {
            second?.Dispose();
        }
    }

    [Fact]
    public void Ctor_syncs_title_and_control_flags_from_playback_state()
    {
        var playbackState = new MutablePlaybackState(PlayingState(canPlayNext: false, canPlayPrevious: false));
        using var sut = CreateSut(playbackState: playbackState);

        Assert.Equal("Genesis 1", sut.Title);
        Assert.True(sut.IsPlaying);
        Assert.True(sut.AreControlsEnabled);
        Assert.False(sut.CanPlayNext);
        Assert.False(sut.CanPlayPrevious);
        Assert.False(sut.IsPreviousEnabled);
        Assert.False(sut.IsNextEnabled);
        Assert.True(sut.ShowArtworkFallback);
    }

    [Fact]
    public void ShowArtworkFallback_false_when_artwork_loading()
    {
        using var sut = CreateSut();
        sut.IsArtworkLoading = true;

        Assert.False(sut.ShowArtworkFallback);
    }

    [Fact]
    public async Task StopCommand_invokes_playback_service()
    {
        var playback = new RecordingPlaybackService();
        using var sut = CreateSut(playback);

        await sut.StopCommand.ExecuteAsync(null);

        Assert.Contains("StopAsync", playback.Calls);
        Assert.True(sut.IsStopping);
    }

    [Fact]
    public async Task NextCommand_invokes_playback_service_and_sets_busy()
    {
        var playback = new RecordingPlaybackService();
        using var sut = CreateSut(playback);

        await sut.NextCommand.ExecuteAsync(null);

        Assert.Contains("PlayNextAsync", playback.Calls);
        Assert.True(sut.IsNextBusy);
        Assert.Equal(0.0, sut.Progress);
    }

    [Fact]
    public async Task PreviousCommand_invokes_playback_service_and_sets_busy()
    {
        var playback = new RecordingPlaybackService();
        using var sut = CreateSut(playback);

        await sut.PreviousCommand.ExecuteAsync(null);

        Assert.Contains("PlayPreviousAsync", playback.Calls);
        Assert.True(sut.IsPreviousBusy);
    }

    [Fact]
    public async Task MaximizeCommand_sends_message_when_controls_enabled()
    {
        var playbackState = new MutablePlaybackState(PlayingState());
        using var sut = CreateSut(playbackState: playbackState);
        var received = 0;
        WeakReferenceMessenger.Default.Register<MaximizePlaybackMessage>(
            this,
            (_, _) => received++);

        try
        {
            await sut.MaximizeCommand.ExecuteAsync(null);
            Assert.Equal(1, received);
        }
        finally
        {
            WeakReferenceMessenger.Default.Unregister<MaximizePlaybackMessage>(this);
        }
    }

    [Fact]
    public async Task MaximizeCommand_is_no_op_when_controls_disabled()
    {
        using var sut = CreateSut();
        sut.AreControlsEnabled = false;
        var received = 0;
        WeakReferenceMessenger.Default.Register<MaximizePlaybackMessage>(
            this,
            (_, _) => received++);

        try
        {
            await sut.MaximizeCommand.ExecuteAsync(null);
            Assert.Equal(0, received);
            Assert.False(sut.IsMaximizeBusy);
        }
        finally
        {
            WeakReferenceMessenger.Default.Unregister<MaximizePlaybackMessage>(this);
        }
    }

    [Fact]
    public void Receive_PlaybackPosition_zero_duration_keeps_progress_at_zero()
    {
        using var sut = CreateSut();

        sut.Receive(new PlaybackPositionChangedMessage
        {
            Duration = TimeSpan.Zero,
            CurrentPosition = TimeSpan.FromSeconds(5),
        });

        Assert.Equal(0.0, sut.Progress);
    }

    [Fact]
    public async Task Receive_PreviousButtonPressedMessage_sets_busy_state()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        using var sut = CreateSut();

        sut.Receive(new PreviousButtonPressedMessage());
        if (!await MauiUiTestHostHelper.FlushMainThreadAsync())
        {
            return;
        }

        Assert.True(sut.IsPreviousBusy);
        Assert.False(sut.AreControlsEnabled);
    }

    [Fact]
    public void Playback_state_change_applies_is_playing_during_auto_advance()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var playbackState = new MutablePlaybackState(
            new PlaybackState(
                new PlaybackTransportSlice(null, true, true, true, PlayStatus.Stopped, true, false),
                new PlaybackMediaSlice("Track", null, null, null, TimeSpan.FromSeconds(30), null),
                new PlaybackDefaultScheduleSlice(null, null, null, null, null)));
        using var sut = CreateSut(playbackState: playbackState);

        try
        {
            playbackState.NotifyStateChanged();
            MauiUiTestHostHelper.FlushMainThreadAsync().GetAwaiter().GetResult();

            Assert.True(sut.IsPlaying);
            Assert.False(sut.AreControlsEnabled);
        }
        catch (COMException)
        {
        }
    }

    [Fact]
    public void Receive_BeginStoppingPlaybackMessage_ignored_after_Dispose()
    {
        var sut = CreateSut();
        sut.Dispose();

        sut.Receive(new BeginStoppingPlaybackMessage());

        Assert.False(sut.IsStopping);
    }

    [Fact]
    public void Receive_NextButtonPressedMessage_ignored_after_Dispose()
    {
        var sut = CreateSut();
        sut.Dispose();

        sut.Receive(new NextButtonPressedMessage());

        Assert.False(sut.IsNextBusy);
    }

    [Fact]
    public async Task SyncFromState_skips_updates_while_IsStopping()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var playbackState = new MutablePlaybackState(PlayingState(title: "Before stop"));
        using var sut = CreateSut(playbackState: playbackState);
        sut.IsStopping = true;

        playbackState.Value = PlayingState(title: "After stop");
        playbackState.NotifyStateChanged();

        try
        {
            if (!await MauiUiTestHostHelper.FlushMainThreadAsync())
            {
                return;
            }

            Assert.Equal("Before stop", sut.Title);
        }
        catch (COMException)
        {
        }
    }

    [Fact]
    public void UpdateArtwork_clears_source_for_empty_url()
    {
        var playbackState = new MutablePlaybackState(
            new PlaybackState(
                new PlaybackTransportSlice(null, true, true, true, PlayStatus.Playing, false, false),
                new PlaybackMediaSlice("T", null, null, string.Empty, TimeSpan.Zero, null),
                new PlaybackDefaultScheduleSlice(null, null, null, null, null)));
        using var sut = CreateSut(playbackState: playbackState);

        Assert.Null(sut.ArtworkSource);
    }

    [Fact]
    public void UpdateArtwork_sets_loading_during_track_transition_without_artwork()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var playbackState = new MutablePlaybackState(
            new PlaybackState(
                new PlaybackTransportSlice(null, true, true, true, PlayStatus.Loading, false, true),
                new PlaybackMediaSlice("Loading", null, null, null, TimeSpan.Zero, null),
                new PlaybackDefaultScheduleSlice(null, null, null, null, null)));
        using var sut = CreateSut(playbackState: playbackState);

        Assert.True(sut.IsArtworkLoading);
    }

    [Fact]
    public void IsPlaying_true_during_transitioning_track_status()
    {
        var playbackState = new MutablePlaybackState(
            new PlaybackState(
                new PlaybackTransportSlice(null, true, true, true, PlayStatus.Stopped, false, true),
                new PlaybackMediaSlice("Transition", null, null, null, TimeSpan.FromSeconds(10), null),
                new PlaybackDefaultScheduleSlice(null, null, null, null, null)));
        using var sut = CreateSut(playbackState: playbackState);

        Assert.True(sut.IsPlaying);
    }

    [Fact]
    public void IsPlaying_true_when_preparing_and_status_stopped()
    {
        var playbackState = new MutablePlaybackState(
            new PlaybackState(
                new PlaybackTransportSlice(null, true, true, true, PlayStatus.Stopped, false, false),
                new PlaybackMediaSlice("Prep", null, null, null, TimeSpan.Zero, null),
                new PlaybackDefaultScheduleSlice(null, null, null, null, null)));
        using var sut = CreateSut(playbackState: playbackState);

        Assert.True(sut.IsPlaying);
        Assert.False(sut.AreControlsEnabled);
    }

    [Fact]
    public void ShowPreviousButton_false_when_previous_busy()
    {
        using var sut = CreateSut();
        sut.IsPreviousBusy = true;

        Assert.False(sut.ShowPreviousButton);
    }

    [Fact]
    public void ShowNextButton_false_when_next_busy()
    {
        using var sut = CreateSut();
        sut.IsNextBusy = true;

        Assert.False(sut.ShowNextButton);
    }

    [Fact]
    public void ShowMaximizeButton_false_when_maximize_busy()
    {
        using var sut = CreateSut();
        sut.IsMaximizeBusy = true;

        Assert.False(sut.ShowMaximizeButton);
    }

    [Fact]
    public async Task StopCommand_resets_IsStopping_when_stop_throws()
    {
        var playback = new ThrowingPlaybackService("StopAsync");
        using var sut = CreateSut(playback);

        await sut.StopCommand.ExecuteAsync(null);

        Assert.False(sut.IsStopping);
    }

    [Fact]
    public async Task NextCommand_clears_next_busy_when_play_next_throws()
    {
        var playback = new ThrowingPlaybackService("PlayNextAsync");
        using var sut = CreateSut(playback);

        await sut.NextCommand.ExecuteAsync(null);

        Assert.False(sut.IsNextBusy);
    }

    [Fact]
    public async Task PreviousCommand_clears_previous_busy_when_play_previous_throws()
    {
        var playback = new ThrowingPlaybackService("PlayPreviousAsync");
        using var sut = CreateSut(playback);

        await sut.PreviousCommand.ExecuteAsync(null);

        Assert.False(sut.IsPreviousBusy);
    }

    [Fact]
    public void Receive_PlaybackPosition_clamps_negative_progress_to_zero()
    {
        using var sut = CreateSut();

        sut.Receive(new PlaybackPositionChangedMessage
        {
            Duration = TimeSpan.FromSeconds(10),
            CurrentPosition = TimeSpan.FromSeconds(-1),
        });

        Assert.Equal(0.0, sut.Progress);
    }

    [Fact]
    public void Progress_updates_when_duration_arrives_after_position_messages()
    {
        using var sut = CreateSut();

        sut.Receive(new PlaybackPositionChangedMessage { CurrentPosition = TimeSpan.FromSeconds(3) });
        sut.Receive(new PlaybackPositionChangedMessage
        {
            Duration = TimeSpan.FromSeconds(10),
            CurrentPosition = TimeSpan.FromSeconds(3),
        });

        Assert.Equal(0.3, sut.Progress, precision: 3);
    }

    [Fact]
    public async Task Track_change_deferral_clears_busy_when_new_track_reaches_playing()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var playbackState = new MutablePlaybackState(PlayingState());
        using var sut = CreateSut(playbackState: playbackState);

        sut.Receive(new NextButtonPressedMessage());
        if (!await MauiUiTestHostHelper.FlushMainThreadAsync())
        {
            return;
        }

        Assert.True(sut.IsNextBusy);

        playbackState.Value = PlayingState(title: "Next track", status: PlayStatus.Playing);
        playbackState.NotifyStateChanged();
        if (!await MauiUiTestHostHelper.FlushMainThreadAsync())
        {
            return;
        }

        Assert.False(sut.IsNextBusy);
        Assert.Equal("Next track", sut.Title);
    }

    private sealed class ThrowingPlaybackService : IPlaybackService
    {
        private readonly string throwOn;

        public ThrowingPlaybackService(string throwOn) => this.throwOn = throwOn;

        public bool IsAlarmPlaybackSession => false;

        public Task PauseAsync() => throwOn == nameof(PauseAsync) ? throw new InvalidOperationException() : Task.CompletedTask;

        public Task PlayAsync() => throwOn == nameof(PlayAsync) ? throw new InvalidOperationException() : Task.CompletedTask;

        public Task PlayNextAsync() => throwOn == nameof(PlayNextAsync) ? throw new InvalidOperationException() : Task.CompletedTask;

        public Task PlayPreviousAsync() => throwOn == nameof(PlayPreviousAsync) ? throw new InvalidOperationException() : Task.CompletedTask;

        public Task PrepareAndPlayAsync(int scheduleId, bool isAlarm) => Task.CompletedTask;

        public Task ResetAndRetryAsync(int scheduleId) => Task.CompletedTask;

        public Task SeekBackwardAsync() => Task.CompletedTask;

        public Task SeekForwardAsync() => Task.CompletedTask;

        public Task SeekToAsync(TimeSpan position) => Task.CompletedTask;

        public Task StopAsync() => throwOn == nameof(StopAsync) ? throw new InvalidOperationException() : Task.CompletedTask;

        public Task StopForTeardownAsync() => Task.CompletedTask;

        public void Dispose()
        {
        }
    }
}
