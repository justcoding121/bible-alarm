#nullable enable

using System.Runtime.CompilerServices;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Shared;
using Fluxor;

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
}
