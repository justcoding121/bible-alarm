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
    private static readonly object Gate = new();

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
        lock (Gate)
        {
            playback ??= new RecordingPlaybackService();
            playbackState ??= new MutablePlaybackState(new PlaybackState());
            return new MiniPlaybackBarViewModel(TestLogging.CreateLogger(), playback, playbackState);
        }
    }

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
}
