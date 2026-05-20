#nullable enable

using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class TrackOnDemandPreparerTests
{
    private sealed class StubPreparePlayback : IPreparePlaybackService
    {
        public Func<PlayItem, CancellationToken, Task<AudioPlayerTrack?>>? PrepareSingleImpl { get; set; }

        public Task<List<AudioPlayerTrack>?> PrepareTracksAsync(int scheduleId, CancellationToken cancellationToken = default) =>
            Task.FromResult<List<AudioPlayerTrack>?>(null);

        public Task<AudioPlayerTrack?> PrepareSingleTrackAsync(PlayItem playItem, CancellationToken cancellationToken = default) =>
            PrepareSingleImpl != null
                ? PrepareSingleImpl(playItem, cancellationToken)
                : Task.FromResult<AudioPlayerTrack?>(null);
    }

    private sealed class StubMediaCache : IMediaCacheService
    {
        public Func<PlayItem, int, CancellationToken, Task<bool>>? CacheTrackImpl { get; set; }

        public void Dispose()
        {
        }

        public Task<bool> ExistsAsync(string lookUpPath, int scheduleId) => Task.FromResult(false);

        public string GetCacheFileName(string lookUpPath) => lookUpPath;

        public string GetCacheFilePath(string lookUpPath, int scheduleId) => lookUpPath;

        public Task<bool> SetupAlarmCacheAsync(int alarmScheduleId) => Task.FromResult(true);

        public Task CleanUpAsync() => Task.CompletedTask;

        public Task<string?> ResolveTrackUriAsync(PlayItem playItem, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<bool> CacheTrackAsync(PlayItem playItem, int scheduleId, CancellationToken cancellationToken = default) =>
            CacheTrackImpl != null
                ? CacheTrackImpl(playItem, scheduleId, cancellationToken)
                : Task.FromResult(false);

        public Task DeleteScheduleCacheAsync(int scheduleId) => Task.CompletedTask;
    }

    private static PlayItem Pi(long scheduleId = 40) =>
        new(new TrackMetadata
        {
            ScheduleId = scheduleId,
            IsBibleContent = true,
            PublicationCode = "nwt",
            TrackCode = "3",
            LookUpPath = "test/stub",
        }, "https://prep");

    [Fact]
    public async Task EnsureTrackPreparedAsync_true_without_calling_prepare_when_uri_present()
    {
        var prepare = new StubPreparePlayback
        {
            PrepareSingleImpl = (_, _) => throw new InvalidOperationException("should not prepare"),
        };
        var sut = new TrackOnDemandPreparer(prepare, new StubMediaCache(), TestLogging.CreateLogger());
        var playItem = Pi();
        var track = new AudioPlayerTrack { PlayItem = playItem, Uri = "https://existing" };

        Assert.True(await sut.EnsureTrackPreparedAsync(track, CancellationToken.None));
    }

    [Fact]
    public async Task EnsureTrackPreparedAsync_resolves_uri_from_prepare_and_returns_false_when_unresolved()
    {
        var playItem = Pi();
        var prepare = new StubPreparePlayback
        {
            PrepareSingleImpl = (p, _) =>
            {
                Assert.Same(playItem, p);
                return Task.FromResult<AudioPlayerTrack?>(new AudioPlayerTrack { Uri = "file://x", PlayItem = p });
            },
        };
        var sut = new TrackOnDemandPreparer(prepare, new StubMediaCache(), TestLogging.CreateLogger());
        var track = new AudioPlayerTrack { PlayItem = playItem, Uri = string.Empty };

        Assert.True(await sut.EnsureTrackPreparedAsync(track, CancellationToken.None));
        Assert.Equal("file://x", track.Uri);

        prepare.PrepareSingleImpl = (_, _) => Task.FromResult<AudioPlayerTrack?>(new AudioPlayerTrack { Uri = string.Empty, PlayItem = playItem });
        track.Uri = string.Empty;
        Assert.False(await sut.EnsureTrackPreparedAsync(track, CancellationToken.None));

        prepare.PrepareSingleImpl = (_, _) => Task.FromResult<AudioPlayerTrack?>(null);
        track.Uri = string.Empty;
        Assert.False(await sut.EnsureTrackPreparedAsync(track, CancellationToken.None));

        prepare.PrepareSingleImpl = (_, _) => Task.FromException<AudioPlayerTrack?>(new FormatException());
        track.Uri = string.Empty;
        Assert.False(await sut.EnsureTrackPreparedAsync(track, CancellationToken.None));

        prepare.PrepareSingleImpl = (_, _) => Task.FromException<AudioPlayerTrack?>(new OperationCanceledException());
        track.Uri = string.Empty;
        Assert.False(await sut.EnsureTrackPreparedAsync(track, CancellationToken.None));
    }

    [Fact]
    public async Task PreDownloadNextTrackAsync_returns_early_when_no_next_or_invalid_schedule_context()
    {
        var sut = new TrackOnDemandPreparer(new StubPreparePlayback(), new StubMediaCache(), TestLogging.CreateLogger());

        await sut.PreDownloadNextTrackAsync(null!, 0, CancellationToken.None);
        await sut.PreDownloadNextTrackAsync([], 0, CancellationToken.None);
        await sut.PreDownloadNextTrackAsync([new AudioPlayerTrack { PlayItem = Pi(), Uri = "a" }], 0, CancellationToken.None);

        var badScheduleId = Pi(0);
        var list = new List<AudioPlayerTrack>
        {
            new() { PlayItem = Pi(), Uri = "a" },
            new() { PlayItem = badScheduleId, Uri = string.Empty },
        };

        await sut.PreDownloadNextTrackAsync(list, 0, CancellationToken.None);
        Assert.True(string.IsNullOrEmpty(list[1].Uri));
    }

    [Fact]
    public async Task PreDownloadNextTrackAsync_sets_next_uri_when_cache_then_prepare_succeed()
    {
        var nextPlayItem = Pi(9);
        var prepare = new StubPreparePlayback
        {
            PrepareSingleImpl = (p, _) =>
                Task.FromResult<AudioPlayerTrack?>(new AudioPlayerTrack { Uri = "file://next", PlayItem = p }),
        };
        var cache = new StubMediaCache
        {
            CacheTrackImpl = (_, _, _) => Task.FromResult(true),
        };
        var sut = new TrackOnDemandPreparer(prepare, cache, TestLogging.CreateLogger());

        var list = new List<AudioPlayerTrack>
        {
            new() { PlayItem = Pi(9), Uri = "https://cur" },
            new() { PlayItem = nextPlayItem, Uri = string.Empty },
        };

        await sut.PreDownloadNextTrackAsync(list, 0, CancellationToken.None);

        Assert.Equal("file://next", list[1].Uri);
    }

    [Fact]
    public async Task PreDownloadNextTrackAsync_returns_when_next_play_item_null()
    {
        var sut = new TrackOnDemandPreparer(new StubPreparePlayback(), new StubMediaCache(), TestLogging.CreateLogger());
        var list = new List<AudioPlayerTrack>
        {
            new() { PlayItem = Pi(), Uri = "a" },
            new() { PlayItem = null!, Uri = string.Empty },
        };

        await sut.PreDownloadNextTrackAsync(list, 0, CancellationToken.None);

        Assert.True(string.IsNullOrEmpty(list[1].Uri));
    }

    [Fact]
    public async Task PreDownloadNextTrackAsync_swallows_operation_canceled_from_cache()
    {
        var cache = new StubMediaCache
        {
            CacheTrackImpl = (_, _, _) => throw new OperationCanceledException(),
        };
        var sut = new TrackOnDemandPreparer(new StubPreparePlayback(), cache, TestLogging.CreateLogger());
        var list = new List<AudioPlayerTrack>
        {
            new() { PlayItem = Pi(), Uri = "a" },
            new() { PlayItem = Pi(9), Uri = string.Empty },
        };

        await sut.PreDownloadNextTrackAsync(list, 0, CancellationToken.None);
    }

    [Fact]
    public async Task PreDownloadNextTrackAsync_swallows_non_critical_cache_failures()
    {
        var cache = new StubMediaCache
        {
            CacheTrackImpl = (_, _, _) => throw new InvalidOperationException("cache failed"),
        };
        var sut = new TrackOnDemandPreparer(new StubPreparePlayback(), cache, TestLogging.CreateLogger());
        var list = new List<AudioPlayerTrack>
        {
            new() { PlayItem = Pi(), Uri = "a" },
            new() { PlayItem = Pi(9), Uri = string.Empty },
        };

        await sut.PreDownloadNextTrackAsync(list, 0, CancellationToken.None);
    }
}
