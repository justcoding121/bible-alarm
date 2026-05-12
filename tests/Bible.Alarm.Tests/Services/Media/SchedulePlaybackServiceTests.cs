#nullable enable

using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Tests.Support;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Tests;

public sealed class SchedulePlaybackServiceTests
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

    private sealed class FakePlaybackState : IState<PlaybackState>
    {
        public FakePlaybackState(PlaybackState value) => Value = value;

        public PlaybackState Value { get; }

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class RecordingToastService : IToastService
    {
        public List<(string Message, int Seconds)> Messages { get; } = [];

        public Task Clear() => Task.CompletedTask;

        public void Dispose()
        {
        }

        public Task ShowMessage(string message, int seconds = 3)
        {
            Messages.Add((message, seconds));
            return Task.CompletedTask;
        }

        public Task ShowScheduledNotification(Bible.Alarm.Shared.Models.Schedule.AlarmSchedule schedule, int seconds = 3) =>
            Task.CompletedTask;
    }

    private sealed class ScopeStub : IServiceScope
    {
        public ScopeStub(IServiceProvider provider) => ServiceProvider = provider;

        public IServiceProvider ServiceProvider { get; }

        public void Dispose()
        {
        }
    }

    private sealed class ScopeFactoryStub : IServiceScopeFactory
    {
        private readonly IServiceScope _scope;

        public ScopeFactoryStub(IServiceScope scope) => _scope = scope;

        public IServiceScope CreateScope() => _scope;
    }

    private static SchedulePlaybackService Sut(IToastService toast, IState<PlaybackState> playbackState) =>
        new(TestLogging.CreateLogger(),
            new ScopeFactoryStub(new ScopeStub(new ServiceCollection()
                .AddSingleton(toast)
                .BuildServiceProvider())),
            playbackState,
            new RecordingDispatcher());

    [Fact]
    public async Task PlayScheduleAsync_zero_or_negative_returns_immediately_without_lock()
    {
        var toast = new RecordingToastService();
        var sut = Sut(toast, new FakePlaybackState(new PlaybackState()));

        await sut.PlayScheduleAsync(0);
        await sut.PlayScheduleAsync(-3);

        Assert.Empty(toast.Messages);
    }

    [Fact]
    public async Task CanMoveTrackAsync_true_when_idle_or_when_playing_other_schedule_without_toast()
    {
        var toast = new RecordingToastService();
        var idle = new FakePlaybackState(new PlaybackState());

        Assert.True(await Sut(toast, idle).CanMoveTrackAsync(404));
        Assert.Empty(toast.Messages);

        var otherSchedule = new FakePlaybackState(new PlaybackState
        {
            IsPreparingOrPlaying = true,
            CurrentScheduleId = 8,
            Status = PlayStatus.Playing,
        });

        Assert.True(await Sut(toast, otherSchedule).CanMoveTrackAsync(99));
        Assert.Empty(toast.Messages);
    }

    [Fact]
    public async Task CanMoveTrackAsync_false_and_shows_toast_when_same_schedule_mid_playback()
    {
        var toast = new RecordingToastService();
        var state = new FakePlaybackState(new PlaybackState
        {
            IsPreparingOrPlaying = true,
            CurrentScheduleId = 55,
            Status = PlayStatus.Playing,
        });

        Assert.False(await Sut(toast, state).CanMoveTrackAsync(55));
        Assert.Single(toast.Messages);
    }
}
