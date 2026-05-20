#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class PlaybackStateManagerTests
{
    private sealed class StubAudioPlayer : IAudioPlayer
    {
        public bool IsActuallyPlayingOrPaused { get; set; }

        public PlayStatus Status { get; set; } = PlayStatus.Stopped;

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

        public TimeSpan? CurrentPosition => null;

        public TimeSpan Duration => TimeSpan.Zero;

        public event EventHandler<EventArgs>? MediaEnded
        {
            add { }
            remove { }
        }

        public event EventHandler<EventArgs>? MediaFailed
        {
            add { }
            remove { }
        }

        public void Dispose()
        {
        }
    }

    [Fact]
    public void IsPreparingOrPlaying_true_when_preparing_flag_set_even_if_player_idle()
    {
        var audio = new StubAudioPlayer();
        var sut = new PlaybackStateManager(TestLogging.CreateLogger())
        {
            IsPreparingTrack = true,
        };

        Assert.True(sut.IsPreparingOrPlaying(audio));
    }

    [Fact]
    public void IsPreparingOrPlaying_follows_audio_player_when_not_preparing()
    {
        var audio = new StubAudioPlayer
        {
            IsActuallyPlayingOrPaused = false,
            Status = PlayStatus.Stopped,
        };
        var sut = new PlaybackStateManager(TestLogging.CreateLogger());

        Assert.False(sut.IsPreparingOrPlaying(audio));

        audio.Status = PlayStatus.Loading;
        Assert.True(sut.IsPreparingOrPlaying(audio));

        audio.IsActuallyPlayingOrPaused = true;
        audio.Status = PlayStatus.Stopped;
        Assert.True(sut.IsPreparingOrPlaying(audio));

        audio.IsActuallyPlayingOrPaused = false;
        audio.Status = PlayStatus.Playing;
        Assert.True(sut.IsPreparingOrPlaying(audio));

        audio.Status = PlayStatus.Paused;
        Assert.True(sut.IsPreparingOrPlaying(audio));
    }

    [Fact]
    public void NotifyPlaybackEstablishedForTrack_tracks_index_for_IsPlaybackEstablishedForTrack()
    {
        var sut = new PlaybackStateManager(TestLogging.CreateLogger());

        Assert.False(sut.IsPlaybackEstablishedForTrack(3));

        sut.NotifyPlaybackEstablishedForTrack(3);

        Assert.True(sut.IsPlaybackEstablishedForTrack(3));
        Assert.False(sut.IsPlaybackEstablishedForTrack(2));
    }

    [Fact]
    public void Reset_clears_session_fields_and_disposes_preparation_cts()
    {
        using var cts = new CancellationTokenSource();
        var sut = new PlaybackStateManager(TestLogging.CreateLogger())
        {
            CurrentScheduleId = 9,
            Playlist = [new AudioPlayerTrack { Uri = "x" }],
            CurrentTrackIndex = 2,
            IsAlarm = true,
            IsIndefinitePlayback = true,
            ManualNavigationPending = true,
            IsPreparingTrack = true,
            PreparationCancellationTokenSource = cts,
        };

        sut.PlayedBibleTrackKeys.Add("k");
        sut.ManuallyVisitedTrackIndices.Add(1);
        sut.NotifyPlaybackEstablishedForTrack(4);

        sut.Reset();

        Assert.Null(sut.CurrentScheduleId);
        Assert.Null(sut.Playlist);
        Assert.Equal(-1, sut.CurrentTrackIndex);
        Assert.False(sut.IsAlarm);
        Assert.False(sut.IsIndefinitePlayback);
        Assert.False(sut.ManualNavigationPending);
        Assert.False(sut.IsPreparingTrack);
        Assert.Empty(sut.PlayedBibleTrackKeys);
        Assert.Empty(sut.ManuallyVisitedTrackIndices);
        Assert.Null(sut.PreparationCancellationTokenSource);
        Assert.False(sut.IsPlaybackEstablishedForTrack(4));
    }

    [Fact]
    public void Reset_swallows_errors_when_preparation_cts_already_disposed()
    {
        var disposed = new CancellationTokenSource();
        disposed.Dispose();
        var sut = new PlaybackStateManager(TestLogging.CreateLogger())
        {
            PreparationCancellationTokenSource = disposed,
        };

        sut.Reset();

        Assert.Null(sut.PreparationCancellationTokenSource);
    }

    [Fact]
    public void Reset_swallows_dispose_when_preparation_cts_disposed_during_reset()
    {
        using var cts = new CancellationTokenSource();
        var sut = new PlaybackStateManager(TestLogging.CreateLogger())
        {
            PreparationCancellationTokenSource = cts,
        };

        cts.Dispose();

        sut.Reset();

        Assert.Null(sut.PreparationCancellationTokenSource);
    }

    [Fact]
    public void Reset_logs_warning_when_preparation_cts_dispose_throws()
    {
        var events = new List<Serilog.Events.LogEvent>();
        var logger = new Serilog.LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(new ListSink(events))
            .CreateLogger();

        var cts = new ThrowOnDisposeCancellationTokenSource();
        var sut = new PlaybackStateManager(logger)
        {
            PreparationCancellationTokenSource = cts,
        };

        sut.Reset();

        Assert.Contains(events, e => e.Level == Serilog.Events.LogEventLevel.Warning);
    }

    private sealed class ListSink(List<Serilog.Events.LogEvent> events) : Serilog.Core.ILogEventSink
    {
        public void Emit(Serilog.Events.LogEvent logEvent) => events.Add(logEvent);
    }

    private sealed class ThrowOnDisposeCancellationTokenSource : CancellationTokenSource
    {
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                throw new InvalidOperationException("dispose failed for test");
            }

            base.Dispose(disposing);
        }
    }
}
