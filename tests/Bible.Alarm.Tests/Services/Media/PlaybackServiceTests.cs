#nullable enable

using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Stores;
using Bible.Alarm.Tests.Support;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class PlaybackServiceTests
{
#pragma warning disable CS0067
    private sealed class NopDispatcher : IDispatcher
    {
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
        }
    }
#pragma warning restore CS0067

    private sealed class FakePlaybackState(PlaybackState value) : IState<PlaybackState>
    {
        public PlaybackState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class StubAudioPlayer : IAudioPlayer
    {
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

        public PlayStatus Status => PlayStatus.Stopped;

        public bool IsActuallyPlayingOrPaused => false;

#pragma warning disable CS0067
        public event EventHandler<EventArgs>? MediaEnded;
        public event EventHandler<EventArgs>? MediaFailed;
#pragma warning restore CS0067
    }

    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var injection = new PlaybackServiceInjectionContext(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            new SyncMainThreadScheduler());

        PlaybackService sut = null!;
        try
        {
            sut = new PlaybackService(
                TestLogging.CreateLogger(),
                new StubAudioPlayer(),
                null!,
                new NopDispatcher(),
                new FakePlaybackState(new PlaybackState()),
                injection);

            Assert.NotNull(sut);
        }
        finally
        {
            sut?.Dispose();
        }
    }
}
