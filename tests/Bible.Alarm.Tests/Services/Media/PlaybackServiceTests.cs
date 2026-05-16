#nullable enable

using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
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

    private sealed class IdleAlarmScheduleService : IAlarmScheduleService
    {
        public void Dispose()
        {
        }

        public Task<List<AlarmSchedule>> GetAllSchedulesAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AlarmSchedule>());

        public Task<List<AlarmSchedule>> GetSchedulesAsync(
            System.Linq.Expressions.Expression<Func<AlarmSchedule, bool>>? predicate = null,
            bool includeMusic = true,
            bool includeBiblePublication = true,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AlarmSchedule>());

        public Task<AlarmSchedule?> GetScheduleByIdAsync(int scheduleId, bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmSchedule?>(null);

        public Task<AlarmSchedule?> GetFirstScheduleOrDefaultAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmSchedule?>(null);

        public Task<AlarmSchedule> AddScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(schedule);

        public Task<AlarmSchedule> UpdateScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(schedule);

        public Task<AlarmSchedule> UpdateScheduleByIdAsync(int scheduleId,
            Action<AlarmSchedule> updateAction,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AlarmSchedule());

        public Task DeleteScheduleAsync(int scheduleId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> ScheduleExistsAsync(int scheduleId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> AnySchedulesExistAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<AlarmMusic?> GetMusicByScheduleIdAsync(int scheduleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmMusic?>(null);

        public Task<BiblePublicationSchedule?> GetBiblePublicationByScheduleIdAsync(int scheduleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublicationSchedule?>(null);
    }

    private sealed class RecordingAudioPlayer : IAudioPlayer
    {
        public int PauseCallCount { get; private set; }

        public void Dispose()
        {
        }

        public Task PrepareAsync(AudioPlayerTrack track) => Task.CompletedTask;

        public Task PlayAsync() => Task.CompletedTask;

        public Task PauseAsync()
        {
            PauseCallCount++;
            return Task.CompletedTask;
        }

        public Task ResumeAsync() => Task.CompletedTask;

        public Task StopAsync() => Task.CompletedTask;

        public Task ResetAsync() => Task.CompletedTask;

        public Task SeekToAsync(TimeSpan position) => Task.CompletedTask;

        public Task SetMutedAsync(bool muted) => Task.CompletedTask;

        public void NotifyTrackTransitionStarting()
        {
        }

        public Task SyncMetadataForTrackAsync(AudioPlayerTrack track) => Task.CompletedTask;

        public TimeSpan? CurrentPosition => null;

        public TimeSpan Duration => TimeSpan.Zero;

        public PlayStatus Status { get; set; } = PlayStatus.Stopped;

        public bool IsActuallyPlayingOrPaused => Status is PlayStatus.Playing or PlayStatus.Paused;

#pragma warning disable CS0067
        public event EventHandler<EventArgs>? MediaEnded;
        public event EventHandler<EventArgs>? MediaFailed;
#pragma warning restore CS0067
    }

    private static PlaybackService CreateSut(
        IAudioPlayer audioPlayer,
        IState<PlaybackState> playbackState) =>
        new(
            TestLogging.CreateLogger(),
            audioPlayer,
            new IdleAlarmScheduleService(),
            new NopDispatcher(),
            playbackState,
            new PlaybackServiceInjectionContext(
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                new SyncMainThreadScheduler()));

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
                new RecordingAudioPlayer(),
                new IdleAlarmScheduleService(),
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

    [Fact]
    public void Receive_PauseButtonPressedMessage_invokes_pause_on_main_thread()
    {
        var player = new RecordingAudioPlayer { Status = PlayStatus.Playing };
        using var sut = CreateSut(player, new FakePlaybackState(new PlaybackState()));

        sut.Receive(new PauseButtonPressedMessage());

        Assert.True(WaitForPause(player, TimeSpan.FromSeconds(5)));
        Assert.Equal(1, player.PauseCallCount);
    }

    [Fact]
    public void Receive_TogglePlayPauseMessage_when_playing_invokes_pause()
    {
        var player = new RecordingAudioPlayer { Status = PlayStatus.Playing };
        using var sut = CreateSut(player, new FakePlaybackState(new PlaybackState { Status = PlayStatus.Playing }));

        sut.Receive(new TogglePlayPauseMessage());

        Assert.True(WaitForPause(player, TimeSpan.FromSeconds(5)));
        Assert.Equal(1, player.PauseCallCount);
    }

    private static bool WaitForPause(RecordingAudioPlayer player, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (player.PauseCallCount > 0)
            {
                return true;
            }

            Thread.Sleep(25);
        }

        return false;
    }

    [Fact]
    public void Receive_NextButtonPressedMessage_does_not_throw_with_empty_playlist()
    {
        using var sut = CreateSut(new RecordingAudioPlayer(), new FakePlaybackState(new PlaybackState()));

        var ex = Record.Exception(() => sut.Receive(new NextButtonPressedMessage()));

        Assert.Null(ex);
    }

    [Fact]
    public void Receive_SeekForwardButtonPressedMessage_does_not_throw()
    {
        using var sut = CreateSut(new RecordingAudioPlayer(), new FakePlaybackState(new PlaybackState()));

        var ex = Record.Exception(() => sut.Receive(new SeekForwardButtonPressedMessage()));

        Assert.Null(ex);
    }
}
