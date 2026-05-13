#nullable enable

using Bible.Alarm.Tests.Support;
using Bible.Alarm.Platforms.Windows.Services.Handlers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores;
using Fluxor;

namespace Bible.Alarm.Tests;

[Trait("Platform", "Windows")]
public sealed class WindowsAlarmHandlerTests
{
    private sealed class RecordingPlaybackService : IPlaybackService
    {
        public int StopAsyncCallCount { get; private set; }
        public List<(int ScheduleId, bool IsAlarm)> PrepareAndPlayCalls { get; } = new();

        public bool IsAlarmPlaybackSession => false;

        public Task PlayAsync() => Task.CompletedTask;

        public Task PauseAsync() => Task.CompletedTask;

        public Task PlayPreviousAsync() => Task.CompletedTask;

        public Task PlayNextAsync() => Task.CompletedTask;

        public Task SeekForwardAsync() => Task.CompletedTask;

        public Task SeekBackwardAsync() => Task.CompletedTask;

        public Task SeekToAsync(TimeSpan position) => Task.CompletedTask;

        public Task PrepareAndPlayAsync(int scheduleId, bool isAlarm)
        {
            PrepareAndPlayCalls.Add((scheduleId, isAlarm));
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            StopAsyncCallCount++;
            return Task.CompletedTask;
        }

        public Task StopForTeardownAsync() => Task.CompletedTask;

        public Task ResetAndRetryAsync(int scheduleId) => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    private sealed class FakePlaybackState(PlaybackState value) : IState<PlaybackState>
    {
        public PlaybackState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private static PlaybackState State(bool isPreparingOrPlaying, PlayStatus status) =>
        new(
            new PlaybackTransportSlice(
                CurrentScheduleId: null,
                IsPreparingOrPlaying: isPreparingOrPlaying,
                CanPlayNext: false,
                CanPlayPrevious: false,
                Status: status,
                IsAutoAdvancing: false,
                IsTransitioningTrack: false),
            new PlaybackMediaSlice(null, null, null, null, TimeSpan.Zero, null),
            new PlaybackDefaultScheduleSlice(null, null, null, null, null));

    [Fact]
    public async Task HandleAsync_invokes_StopAsync_before_PrepareAndPlay_when_playback_active()
    {
        var playback = new RecordingPlaybackService();
        var state = new FakePlaybackState(State(isPreparingOrPlaying: true, status: PlayStatus.Playing));
        var sut = new WindowsAlarmHandler(TestLogging.CreateLogger(), playback, state);

        await sut.HandleAsync(77, isAlarm: true);

        Assert.Equal(1, playback.StopAsyncCallCount);
        Assert.Single(playback.PrepareAndPlayCalls);
        Assert.Equal((77, true), playback.PrepareAndPlayCalls[0]);
    }

    [Fact]
    public async Task HandleAsync_skips_StopAsync_when_not_preparing_or_playing()
    {
        var playback = new RecordingPlaybackService();
        var state = new FakePlaybackState(State(isPreparingOrPlaying: false, status: PlayStatus.Stopped));
        var sut = new WindowsAlarmHandler(TestLogging.CreateLogger(), playback, state);

        await sut.HandleAsync(3, isAlarm: false);

        Assert.Equal(0, playback.StopAsyncCallCount);
        Assert.Single(playback.PrepareAndPlayCalls);
        Assert.Equal((3, false), playback.PrepareAndPlayCalls[0]);
    }
}
