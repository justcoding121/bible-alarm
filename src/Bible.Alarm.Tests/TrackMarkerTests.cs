#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class TrackMarkerTests
{
    private sealed class RecordingPlaylistService : IPlaylistService
    {
        private static readonly BiblePublicationTrack DummyTrack =
            new() { TrackCode = "1", Title = "T" };

        private static readonly BiblePublicationSection DummySection =
            new() { SectionCode = "s", Name = "S" };

        public List<TrackMetadata> MarkedAsPlayed { get; } = [];

        public void Dispose()
        {
        }

        public Task MarkTrackAsPlayed(TrackMetadata trackMetadata)
        {
            MarkedAsPlayed.Add(trackMetadata);
            return Task.CompletedTask;
        }

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
            string? sectionCode, string trackCode) =>
            Task.FromResult(new TrackNavigationResult(publicationCode, null, DummyTrack));

        public Task<KeyValuePair<string, BiblePublicationSection>>
            GetPreviousBiblePublicationSection(string languageCode, string publicationCode, string sectionCode) =>
            Task.FromResult(new KeyValuePair<string, BiblePublicationSection>("k", DummySection));

        public Task<KeyValuePair<string, BiblePublicationSection>> GetNextBiblePublicationSection(string languageCode,
            string publicationCode, string sectionCode) =>
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
                LookUpPath = "/stub"
            };
    }

    private static TrackMetadata Meta(int scheduleId = 5, string trackCode = "3") =>
        new()
        {
            ScheduleId = scheduleId,
            IsBibleContent = true,
            LanguageCode = "E",
            PublicationCode = "nwt",
            SectionCode = "40",
            TrackCode = trackCode,
            LookUpPath = "/t"
        };

    [Fact]
    public async Task MarkTrackAsPlayedAsync_no_op_for_null_or_bad_index()
    {
        var playlists = new RecordingPlaylistService();
        var sut = new TrackMarker(playlists, TestLogging.CreateLogger());

        await sut.MarkTrackAsPlayedAsync(null, 0);
        await sut.MarkTrackAsPlayedAsync([], 0);
        await sut.MarkTrackAsPlayedAsync([MakeTrack()], -1);
        await sut.MarkTrackAsPlayedAsync([MakeTrack()], 1);

        Assert.Empty(playlists.MarkedAsPlayed);
    }

    [Fact]
    public async Task MarkTrackAsPlayedAsync_calls_playlist_for_valid_index()
    {
        var playlists = new RecordingPlaylistService();
        var sut = new TrackMarker(playlists, TestLogging.CreateLogger());
        var meta = Meta();
        var list = new List<AudioPlayerTrack> { new() { PlayItem = new PlayItem(meta, "https://x") } };

        await sut.MarkTrackAsPlayedAsync(list, 0);

        Assert.Single(playlists.MarkedAsPlayed);
        Assert.Same(meta, playlists.MarkedAsPlayed[0]);
    }

    private static AudioPlayerTrack MakeTrack() =>
        new() { PlayItem = new PlayItem(Meta(), "https://y") };
}
