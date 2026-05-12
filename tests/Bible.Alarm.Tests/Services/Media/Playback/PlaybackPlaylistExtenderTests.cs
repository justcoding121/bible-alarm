#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class PlaybackPlaylistExtenderTests
{
    private sealed class StubPlaylistService : IPlaylistService
    {
        public PlayItem NextResponse { get; set; } = new(Meta("2"), "https://next");

        public PlayItem PreviousResponse { get; set; } = new(Meta("0"), "https://prev");

        public Exception? ThrowOnNext { get; set; }

        public Exception? ThrowOnPrevious { get; set; }

        private static TrackMetadata Meta(string trackCode) =>
            new()
            {
                ScheduleId = 1,
                IsBibleContent = true,
                LanguageCode = "E",
                PublicationCode = "nwt",
                SectionCode = "40",
                TrackCode = trackCode,
            };

        private static PlayItem Fallback() => new(Meta("9"), "https://fallback");

        private static TrackNavigationResult DummyNav() =>
            new("nwt", null, new BiblePublicationTrack { TrackCode = "1" });

        public void Dispose()
        {
        }

        public Task MarkTrackAsPlayed(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task MarkTrackAsFinished(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task<PlayItem> NextTrack(int scheduleId) => Task.FromResult(Fallback());

        public Task<PlayItem?> NextBiblePublicationTrack(int scheduleId) => Task.FromResult<PlayItem?>(Fallback());

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

        public Task<bool> ShouldResumeFromLastPositionAsync(int scheduleId) => Task.FromResult(false);

        public Task<TimeSpan> GetScheduleFinishedDurationAsync(int scheduleId) => Task.FromResult(TimeSpan.Zero);

        public Task<PlayItem> GetNextPlayItemAsync(TrackMetadata currentTrackMetadata, IFetchProgress? sectionFetchProgress = null)
        {
            _ = sectionFetchProgress;
            if (ThrowOnNext != null)
            {
                return Task.FromException<PlayItem>(ThrowOnNext);
            }

            return Task.FromResult(NextResponse);
        }

        public Task<PlayItem> GetPreviousPlayItemAsync(TrackMetadata currentTrackMetadata, IFetchProgress? sectionFetchProgress = null)
        {
            _ = sectionFetchProgress;
            if (ThrowOnPrevious != null)
            {
                return Task.FromException<PlayItem>(ThrowOnPrevious);
            }

            return Task.FromResult(PreviousResponse);
        }

        public Task PersistSchedulePointerToFinishedTrackAsync(TrackMetadata trackMetadata) => Task.CompletedTask;
    }

    private static PlaybackPlaylistExtender Sut(StubPlaylistService playlists) =>
        new(new PlaybackIndefiniteResolver(playlists), TestLogging.CreateLogger());

    private static AudioPlayerTrack Track(string code) =>
        new()
        {
            PlayItem = new PlayItem(
                new TrackMetadata
                {
                    ScheduleId = 1,
                    IsBibleContent = true,
                    LanguageCode = "E",
                    PublicationCode = "nwt",
                    SectionCode = "40",
                    TrackCode = code,
                },
                $"https://{code}"),
            Uri = "https://streaming",
        };

    [Fact]
    public async Task TryAppendNextTrackAsync_false_for_invalid_playlist_or_index()
    {
        var playlists = new StubPlaylistService();
        var sut = Sut(playlists);

        Assert.False(await sut.TryAppendNextTrackAsync(null!, 0, null, null, null, null, CancellationToken.None));
        Assert.False(await sut.TryAppendNextTrackAsync([], 0, null, null, null, null, CancellationToken.None));
        Assert.False(await sut.TryAppendNextTrackAsync([Track("1")], -1, null, null, null, null, CancellationToken.None));
        Assert.False(await sut.TryAppendNextTrackAsync([Track("1")], 4, null, null, null, null, CancellationToken.None));
    }

    [Fact]
    public async Task TryAppendNextTrackAsync_appends_empty_uri_track_from_resolver()
    {
        var playlists = new StubPlaylistService();
        var sut = Sut(playlists);
        var list = new List<AudioPlayerTrack> { Track("1") };

        Assert.True(await sut.TryAppendNextTrackAsync(list, 0, null, null, null, null, CancellationToken.None));

        Assert.Equal(2, list.Count);
        Assert.Empty(list[1].Uri);
        Assert.Equal("2", list[1].PlayItem.Metadata.TrackCode);
    }

    [Fact]
    public async Task TryAppendNextTrackAsync_false_on_cancellation_or_resolver_failure()
    {
        var playlists = new StubPlaylistService { ThrowOnNext = new OperationCanceledException() };
        var sut = Sut(playlists);

        Assert.False(await sut.TryAppendNextTrackAsync([Track("1")], 0, null, null, null, null, CancellationToken.None));

        playlists.ThrowOnNext = new FormatException();
        Assert.False(await sut.TryAppendNextTrackAsync([Track("1")], 0, null, null, null, null, CancellationToken.None));
    }

    [Fact]
    public async Task TryPrependPreviousTrackAsync_false_for_invalid_playlist_or_index()
    {
        var playlists = new StubPlaylistService();
        var sut = Sut(playlists);

        Assert.False(await sut.TryPrependPreviousTrackAsync(null!, 0, null, null, null, null, null));
        Assert.False(await sut.TryPrependPreviousTrackAsync([Track("1")], 9, null, null, null, null, null));
    }

    [Fact]
    public async Task TryPrependPreviousTrackAsync_inserts_at_zero_with_empty_uri()
    {
        var playlists = new StubPlaylistService();
        var sut = Sut(playlists);
        var list = new List<AudioPlayerTrack> { Track("1") };

        Assert.True(await sut.TryPrependPreviousTrackAsync(list, 0, 1, null, null, null, null));

        Assert.Equal(2, list.Count);
        Assert.Equal("0", list[0].PlayItem.Metadata.TrackCode);
        Assert.Empty(list[0].Uri);
        Assert.Equal("1", list[1].PlayItem.Metadata.TrackCode);
    }
}
