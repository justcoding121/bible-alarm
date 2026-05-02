#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class TrackPlaybackHandlerTests
{
    private sealed class StubPlaylist : IPlaylistService
    {
        private static readonly BiblePublicationTrack DummyTrack = new() { TrackCode = "1", Title = "T" };
        private static readonly BiblePublicationSection DummySection = new() { SectionCode = "s", Name = "S" };

        public void Dispose()
        {
        }

        public Task MarkTrackAsPlayed(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task MarkTrackAsFinished(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task<PlayItem> NextTrack(int scheduleId) =>
            Task.FromResult(new PlayItem(DummyMeta(), "https://stub"));

        public Task<PlayItem?> NextBiblePublicationTrack(int scheduleId) =>
            Task.FromResult<PlayItem?>(new PlayItem(DummyMeta(), "https://stub"));

        public Task<List<PlayItem>> NextTracks(int scheduleId) =>
            Task.FromResult(new List<PlayItem>());

        public Task SaveLastPlayed(int currentScheduleId) => Task.CompletedTask;

        public Task<int> GetRelevantScheduleToPlay() => Task.FromResult(0);

        public Task MoveToNextBiblePublicationTrack(int scheduleId) => Task.CompletedTask;

        public Task MoveToPreviousBiblePublicationTrack(int scheduleId) => Task.CompletedTask;

        public Task<TrackNavigationResult> GetNextBiblePublicationTrack(string languageCode, string publicationCode,
            string? sectionCode, string trackCode) =>
            Task.FromResult(new TrackNavigationResult(publicationCode, null, DummyTrack));

        public Task<TrackNavigationResult> GetPreviousBiblePublicationTrack(string languageCode,
            string publicationCode,
            string? sectionCode,
            string trackCode) =>
            Task.FromResult(new TrackNavigationResult(publicationCode, null, DummyTrack));

        public Task<KeyValuePair<string, BiblePublicationSection>>
            GetPreviousBiblePublicationSection(string languageCode, string publicationCode, string sectionCode) =>
            Task.FromResult(new KeyValuePair<string, BiblePublicationSection>("k", DummySection));

        public Task<KeyValuePair<string, BiblePublicationSection>> GetNextBiblePublicationSection(string languageCode,
            string publicationCode,
            string sectionCode) =>
            Task.FromResult(new KeyValuePair<string, BiblePublicationSection>("k", DummySection));

        public Task<bool> ShouldResumeFromLastPositionAsync(int scheduleId) => Task.FromResult(false);

        public Task<TimeSpan> GetScheduleFinishedDurationAsync(int scheduleId) => Task.FromResult(TimeSpan.Zero);

        public Task<PlayItem> GetNextPlayItemAsync(TrackMetadata currentTrackMetadata,
            IFetchProgress? sectionFetchProgress = null) =>
            Task.FromResult(new PlayItem(currentTrackMetadata, "https://stub"));

        public Task<PlayItem> GetPreviousPlayItemAsync(TrackMetadata currentTrackMetadata,
            IFetchProgress? sectionFetchProgress = null) =>
            Task.FromResult(new PlayItem(currentTrackMetadata, "https://stub"));

        public Task PersistSchedulePointerToFinishedTrackAsync(TrackMetadata trackMetadata) =>
            Task.CompletedTask;

        private static TrackMetadata DummyMeta() =>
            new()
            {
                ScheduleId = 1,
                IsBibleContent = true,
                LanguageCode = "E",
                PublicationCode = "nwt",
                SectionCode = "40",
                TrackCode = "1",
                LookUpPath = "/stub",
            };
    }

    private sealed class StubAudioPlayer : IAudioPlayer
    {
        public PlayStatus Status { get; set; } = PlayStatus.Stopped;

        public TimeSpan? CurrentPosition { get; set; }

        public TimeSpan Duration => TimeSpan.Zero;

        public bool IsActuallyPlayingOrPaused => false;

#pragma warning disable CS0067
        public event EventHandler<EventArgs>? MediaEnded;

        public event EventHandler<EventArgs>? MediaFailed;
#pragma warning restore CS0067

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

        public void Dispose()
        {
        }
    }

    [Fact]
    public async Task PlayTrackAsync_ReturnsFalse_WhenTrackUriMissing()
    {
        var logger = TestLogging.CreateLogger();
        var audio = new StubAudioPlayer();
        var playlist = new StubPlaylist();
        var preparation = new TrackPreparationHandler(audio, playlist, logger);
        using var progress = new ProgressTracker(playlist, audio, logger);
        var sut = new TrackPlaybackHandler(audio, logger, preparation, progress);

        var meta = new TrackMetadata
        {
            ScheduleId = 10,
            IsBibleContent = false,
            LanguageCode = "E",
            PublicationCode = "osg",
            TrackCode = "1",
            LookUpPath = "/m",
        };
        var track = new AudioPlayerTrack { Uri = "", PlayItem = new PlayItem(meta, "") };

        var preparing = false;
        var request = new PlayTrackRequest(
            track,
            0,
            true,
            null,
            () => preparing,
            () => [],
            v => preparing = v,
            [],
            CancellationToken.None);

        var ok = await sut.PlayTrackAsync(request);

        Assert.False(ok);
        Assert.False(preparing);
    }
}
