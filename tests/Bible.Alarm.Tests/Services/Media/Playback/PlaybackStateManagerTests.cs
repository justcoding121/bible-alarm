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
}
