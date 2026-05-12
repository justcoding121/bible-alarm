#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class TrackPreparationHandlerTests
{
    private sealed class ReadyStubAudioPlayer : IAudioPlayer
    {
        public bool IsActuallyPlayingOrPaused => true;

        public PlayStatus Status => PlayStatus.Playing;

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

    private sealed class BusyStubAudioPlayer : IAudioPlayer
    {
        public bool IsActuallyPlayingOrPaused => false;

        public PlayStatus Status => PlayStatus.Stopped;

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

    private sealed class StubPlaylistService : IPlaylistService
    {
        private static TrackMetadata DummyMeta() =>
            new()
            {
                ScheduleId = 1,
                IsBibleContent = true,
                LanguageCode = "E",
                PublicationCode = "nwt",
                TrackCode = "1",
            };

        private static PlayItem DummyPlayItem() => new(DummyMeta(), "https://example.invalid/track");

        private static TrackNavigationResult DummyNav() =>
            new("nwt", null, new BiblePublicationTrack { TrackCode = "1" });

        public bool ShouldResumeValue { get; set; }

        public TimeSpan FinishedDurationValue { get; set; }

        public Exception? ThrowOnShouldResume { get; set; }

        public Exception? ThrowOnFinishedDuration { get; set; }

        public void Dispose()
        {
        }

        public Task MarkTrackAsPlayed(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task MarkTrackAsFinished(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task<PlayItem> NextTrack(int scheduleId) => Task.FromResult(DummyPlayItem());

        public Task<PlayItem?> NextBiblePublicationTrack(int scheduleId) => Task.FromResult<PlayItem?>(DummyPlayItem());

        public Task<List<PlayItem>> NextTracks(int scheduleId) => Task.FromResult(new List<PlayItem>());

        public Task SaveLastPlayed(int currentScheduleId) => Task.CompletedTask;

        public Task<int> GetRelevantScheduleToPlay() => Task.FromResult(0);

        public Task MoveToNextBiblePublicationTrack(int scheduleId) => Task.CompletedTask;

        public Task MoveToPreviousBiblePublicationTrack(int scheduleId) => Task.CompletedTask;

        public Task<TrackNavigationResult> GetNextBiblePublicationTrack(string languageCode, string publicationCode,
            string? sectionCode, string trackCode) =>
            Task.FromResult(DummyNav());

        public Task<TrackNavigationResult> GetPreviousBiblePublicationTrack(string languageCode, string publicationCode,
            string? sectionCode, string trackCode) =>
            Task.FromResult(DummyNav());

        public Task<KeyValuePair<string, BiblePublicationSection>> GetPreviousBiblePublicationSection(string languageCode,
            string publicationCode, string sectionCode) =>
            Task.FromResult(default(KeyValuePair<string, BiblePublicationSection>));

        public Task<KeyValuePair<string, BiblePublicationSection>> GetNextBiblePublicationSection(string languageCode,
            string publicationCode, string sectionCode) =>
            Task.FromResult(default(KeyValuePair<string, BiblePublicationSection>));

        public Task<bool> ShouldResumeFromLastPositionAsync(int scheduleId)
        {
            _ = scheduleId;
            if (ThrowOnShouldResume != null)
            {
                return Task.FromException<bool>(ThrowOnShouldResume);
            }

            return Task.FromResult(ShouldResumeValue);
        }

        public Task<TimeSpan> GetScheduleFinishedDurationAsync(int scheduleId)
        {
            _ = scheduleId;
            if (ThrowOnFinishedDuration != null)
            {
                return Task.FromException<TimeSpan>(ThrowOnFinishedDuration);
            }

            return Task.FromResult(FinishedDurationValue);
        }

        public Task<PlayItem> GetNextPlayItemAsync(TrackMetadata currentTrackMetadata, IFetchProgress? sectionFetchProgress = null) =>
            Task.FromResult(DummyPlayItem());

        public Task<PlayItem> GetPreviousPlayItemAsync(TrackMetadata currentTrackMetadata, IFetchProgress? sectionFetchProgress = null) =>
            Task.FromResult(DummyPlayItem());

        public Task PersistSchedulePointerToFinishedTrackAsync(TrackMetadata trackMetadata) => Task.CompletedTask;
    }

    [Fact]
    public async Task WaitForMediaReadyAsync_returns_when_player_reports_ready_without_waiting()
    {
        var sut = new TrackPreparationHandler(new ReadyStubAudioPlayer(),
            new StubPlaylistService(),
            TestLogging.CreateLogger());

        await sut.WaitForMediaReadyAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WaitForMediaReadyAsync_throws_when_cancelled_before_loop_advances_with_busy_player()
    {
        var sut = new TrackPreparationHandler(new BusyStubAudioPlayer(),
            new StubPlaylistService(),
            TestLogging.CreateLogger());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.WaitForMediaReadyAsync(cts.Token));
    }

    [Fact]
    public async Task ShouldResumeFromLastPositionAsync_false_when_schedule_id_missing()
    {
        var sut = new TrackPreparationHandler(new ReadyStubAudioPlayer(),
            new StubPlaylistService { ShouldResumeValue = true },
            TestLogging.CreateLogger());

        Assert.False(await sut.ShouldResumeFromLastPositionAsync(null));
    }

    [Fact]
    public async Task ShouldResumeFromLastPositionAsync_returns_playlist_answer_and_swallows_exceptions()
    {
        var playlists = new StubPlaylistService { ShouldResumeValue = true };
        var sut = new TrackPreparationHandler(new ReadyStubAudioPlayer(), playlists, TestLogging.CreateLogger());

        Assert.True(await sut.ShouldResumeFromLastPositionAsync(9));

        playlists.ThrowOnShouldResume = new InvalidOperationException();
        Assert.False(await sut.ShouldResumeFromLastPositionAsync(9));
    }

    [Fact]
    public async Task GetScheduleFinishedDurationAsync_zero_when_schedule_id_missing()
    {
        var sut = new TrackPreparationHandler(new ReadyStubAudioPlayer(),
            new StubPlaylistService { FinishedDurationValue = TimeSpan.FromMinutes(1) },
            TestLogging.CreateLogger());

        Assert.Equal(TimeSpan.Zero, await sut.GetScheduleFinishedDurationAsync(null));
    }

    [Fact]
    public async Task GetScheduleFinishedDurationAsync_returns_playlist_value_and_returns_zero_after_exception()
    {
        var playlists = new StubPlaylistService { FinishedDurationValue = TimeSpan.FromSeconds(90) };
        var sut = new TrackPreparationHandler(new ReadyStubAudioPlayer(), playlists, TestLogging.CreateLogger());

        Assert.Equal(TimeSpan.FromSeconds(90), await sut.GetScheduleFinishedDurationAsync(3));

        playlists.ThrowOnFinishedDuration = new FormatException();
        Assert.Equal(TimeSpan.Zero, await sut.GetScheduleFinishedDurationAsync(3));
    }
}
