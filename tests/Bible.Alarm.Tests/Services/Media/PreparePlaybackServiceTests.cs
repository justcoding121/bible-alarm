#nullable enable

using Bible.Alarm.Services.Media;
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

public sealed class PreparePlaybackServiceTests
{
    private sealed class StubPlaylistService : IPlaylistService
    {
        public Func<int, Task<List<PlayItem>>> NextTracksAsync { get; set; } =
            _ => Task.FromResult(new List<PlayItem>());

        private static TrackMetadata DummyMeta() =>
            new()
            {
                ScheduleId = 1,
                IsBibleContent = true,
                LanguageCode = "E",
                PublicationCode = "nwt",
                SectionCode = "40",
                TrackCode = "1",
                LookUpPath = "/lk",
            };

        private static PlayItem DummyPlayItem() => new(DummyMeta(), "https://cdn/track");

        private static TrackNavigationResult DummyNav() =>
            new("nwt", null, new BiblePublicationTrack { TrackCode = "1" });

        public void Dispose()
        {
        }

        public Task MarkTrackAsPlayed(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task MarkTrackAsFinished(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task<PlayItem> NextTrack(int scheduleId) => Task.FromResult(DummyPlayItem());

        public Task<PlayItem?> NextBiblePublicationTrack(int scheduleId) => Task.FromResult<PlayItem?>(DummyPlayItem());

        public Task<List<PlayItem>> NextTracks(int scheduleId) => NextTracksAsync(scheduleId);

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

        public Task<PlayItem> GetNextPlayItemAsync(TrackMetadata currentTrackMetadata, IFetchProgress? sectionFetchProgress = null) =>
            Task.FromResult(DummyPlayItem());

        public Task<PlayItem> GetPreviousPlayItemAsync(TrackMetadata currentTrackMetadata, IFetchProgress? sectionFetchProgress = null) =>
            Task.FromResult(DummyPlayItem());

        public Task PersistSchedulePointerToFinishedTrackAsync(TrackMetadata trackMetadata) => Task.CompletedTask;
    }

    private sealed class StubMediaCacheService : IMediaCacheService
    {
        public Func<PlayItem, CancellationToken, Task<string?>>? Resolve { get; set; }

        public void Dispose()
        {
        }

        public Task<bool> ExistsAsync(string lookUpPath, int scheduleId) => Task.FromResult(false);

        public string GetCacheFileName(string lookUpPath) => lookUpPath;

        public string GetCacheFilePath(string lookUpPath, int scheduleId) => lookUpPath;

        public Task<bool> SetupAlarmCacheAsync(int alarmScheduleId) => Task.FromResult(true);

        public Task CleanUpAsync() => Task.CompletedTask;

        public Task<string?> ResolveTrackUriAsync(PlayItem playItem, CancellationToken cancellationToken = default) =>
            Resolve != null
                ? Resolve(playItem, cancellationToken)
                : Task.FromResult<string?>(null);

        public Task<bool> CacheTrackAsync(PlayItem playItem, int scheduleId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task DeleteScheduleCacheAsync(int scheduleId) => Task.CompletedTask;
    }

    private static TrackMetadata Meta(int scheduleId = 5) =>
        new()
        {
            ScheduleId = scheduleId,
            IsBibleContent = true,
            LanguageCode = "E",
            PublicationCode = "nwt",
            SectionCode = "1",
            TrackCode = "1",
            LookUpPath = "/p",
        };

    [Fact]
    public async Task PrepareTracksAsync_returns_null_when_next_tracks_throws()
    {
        var playlists = new StubPlaylistService
        {
            NextTracksAsync = _ => Task.FromException<List<PlayItem>>(new InvalidOperationException("db")),
        };
        var sut = new PreparePlaybackService(TestLogging.CreateLogger(), playlists, new StubMediaCacheService());

        Assert.Null(await sut.PrepareTracksAsync(1));
    }

    [Fact]
    public async Task PrepareTracksAsync_returns_empty_list_when_no_play_items()
    {
        var playlists = new StubPlaylistService
        {
            NextTracksAsync = _ => Task.FromResult(new List<PlayItem>()),
        };
        var sut = new PreparePlaybackService(TestLogging.CreateLogger(), playlists, new StubMediaCacheService());

        var result = await sut.PrepareTracksAsync(2);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task PrepareTracksAsync_returns_null_when_first_track_uri_unresolved()
    {
        var item = new PlayItem(Meta(), "https://x");
        var playlists = new StubPlaylistService
        {
            NextTracksAsync = _ => Task.FromResult(new List<PlayItem> { item }),
        };
        var cache = new StubMediaCacheService
        {
            Resolve = (_, _) => Task.FromResult<string?>(null),
        };

        var sut = new PreparePlaybackService(TestLogging.CreateLogger(), playlists, cache);

        Assert.Null(await sut.PrepareTracksAsync(3));
    }

    [Fact]
    public async Task PrepareTracksAsync_returns_tracks_when_uris_resolve()
    {
        var first = new PlayItem(Meta(), "https://a");
        var second = new PlayItem(Meta(), "https://b");
        var playlists = new StubPlaylistService
        {
            NextTracksAsync = _ => Task.FromResult(new List<PlayItem> { first, second }),
        };
        var cache = new StubMediaCacheService
        {
            Resolve = (p, _) => Task.FromResult<string?>("file://" + p.Url),
        };

        var sut = new PreparePlaybackService(TestLogging.CreateLogger(), playlists, cache);

        var result = await sut.PrepareTracksAsync(4);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        Assert.Equal("file://https://a", result[0].Uri);
        Assert.Equal("file://https://b", result[1].Uri);
    }

    [Fact]
    public async Task PrepareSingleTrackAsync_returns_null_when_resolve_returns_null_or_cancelled()
    {
        var item = new PlayItem(Meta(), "https://c");
        var cache = new StubMediaCacheService
        {
            Resolve = (_, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult<string?>(null);
            },
        };
        var sut = new PreparePlaybackService(TestLogging.CreateLogger(), new StubPlaylistService(), cache);

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        Assert.Null(await sut.PrepareSingleTrackAsync(item, cancelled.Token));

        var cacheHitsNull = new StubMediaCacheService
        {
            Resolve = (_, _) => Task.FromResult<string?>(null),
        };

        sut = new PreparePlaybackService(TestLogging.CreateLogger(), new StubPlaylistService(), cacheHitsNull);

        Assert.Null(await sut.PrepareSingleTrackAsync(item));
    }

    [Fact]
    public async Task PrepareTracksAsync_returns_null_when_resolve_throws()
    {
        var item = new PlayItem(Meta(), "https://x");
        var playlists = new StubPlaylistService
        {
            NextTracksAsync = _ => Task.FromResult(new List<PlayItem> { item }),
        };
        var cache = new StubMediaCacheService
        {
            Resolve = (_, _) => throw new InvalidOperationException("cache failure"),
        };

        var sut = new PreparePlaybackService(TestLogging.CreateLogger(), playlists, cache);

        Assert.Null(await sut.PrepareTracksAsync(5));
    }

    [Fact]
    public async Task PrepareSingleTrackAsync_returns_AudioPlayerTrack_when_uri_resolves()
    {
        var item = new PlayItem(Meta(), "https://ok");
        var cache = new StubMediaCacheService
        {
            Resolve = (_, _) => Task.FromResult<string?>("file://local"),
        };
        var sut = new PreparePlaybackService(TestLogging.CreateLogger(), new StubPlaylistService(), cache);

        var track = await sut.PrepareSingleTrackAsync(item);

        Assert.NotNull(track);
        Assert.Same(item, track!.PlayItem);
        Assert.Equal("file://local", track.Uri);
    }
}
