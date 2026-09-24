#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Network.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class MediaCacheServiceTests
{
    private sealed class StorageStub : IStorageService
    {
        public string StorageRoot => @"Y:\alarm-store";

        public string CacheRoot => "cache-root";

        public Task<bool> DirectoryExists(string path) => Task.FromResult(false);

        public Task<bool> FileExists(string path) => Task.FromResult(false);

        public Task<List<string>> GetAllFiles(string path) => Task.FromResult(new List<string>());

        public Task<DateTimeOffset> GetFileCreationDate(string path) =>
            Task.FromResult(DateTimeOffset.UtcNow);

        [RequiresAssemblyFiles]
        public Task<DateTimeOffset> GetFileCreationDateFromResource(string resourceName) =>
            Task.FromResult(DateTimeOffset.UtcNow);

        public Task<string> ReadFile(string path) => Task.FromResult(string.Empty);

        public Task CopyResourceFile(string resourceFileName, string destinationDirectoryPath,
            string destinationFileName) =>
            Task.CompletedTask;

        public Task SaveFile(string directoryPath, string fileName, string contents) => Task.CompletedTask;

        public Task SaveFile(string directoryPath, string fileName, byte[] contents) => Task.CompletedTask;

        public Task DeleteFile(string path) => Task.CompletedTask;

        public Task DeleteDirectory(string path) => Task.CompletedTask;

        public Task<DirectoryInfo> CreateDirectory(string path) =>
            Task.FromResult(new DirectoryInfo(path));

        public void Dispose()
        {
        }
    }

    private sealed class IdleDownload : IDownloadService
    {
        public void Dispose()
        {
        }

        public Task<byte[]> DownloadAsync(string url, string? alternativeUrl = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Array.Empty<byte>());

        public Task<byte[]> DownloadWithProgressAsync(string url,
            Action<long, long?>? progressCallback,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Array.Empty<byte>());

        public Task<long?> GetContentLengthAsync(string url, CancellationToken cancellationToken = default) =>
            Task.FromResult<long?>(null);

        public Task<bool> FileExists(string url) => Task.FromResult(false);
    }

    private sealed class IdleUrlRefresh : IMediaUrlRefreshService
    {
        public Task<string?> RefreshUrlAsync(TrackMetadata trackMetadata) => Task.FromResult<string?>(null);
    }

    private sealed class IdleNetwork : INetworkStatusService
    {
        public Task<bool> IsInternetAvailable() => Task.FromResult(false);
    }

    private sealed class IdleTrackRefresh : ITrackCdnUrlRefresher
    {
        public Task<string?> TryRefreshTrackCdnUrlFromApiAsync(TrackMetadata metadata,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    private sealed class IdlePlaylistService : IPlaylistService
    {
        public void Dispose()
        {
        }

        private static TrackNavigationResult NavDummy() =>
            new("pub", null, new BiblePublicationTrack { TrackCode = "1" });

        public Task MarkTrackAsPlayed(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task MarkTrackAsFinished(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task<PlayItem> NextTrack(int scheduleId) =>
            Task.FromResult(new PlayItem(St(), "url"));

        public Task<PlayItem?> NextBiblePublicationTrack(int scheduleId) =>
            Task.FromResult<PlayItem?>(null);

        public Task<List<PlayItem>> NextTracks(int scheduleId) =>
            Task.FromResult(new List<PlayItem>());

        public Task SaveLastPlayed(int currentScheduleId) => Task.CompletedTask;

        public Task<int> GetRelevantScheduleToPlay() => Task.FromResult(0);

        public Task MoveToNextBiblePublicationTrack(int scheduleId) => Task.CompletedTask;

        public Task MoveToPreviousBiblePublicationTrack(int scheduleId) => Task.CompletedTask;

        public Task<TrackNavigationResult> GetNextBiblePublicationTrack(string languageCode,
            string publicationCode, string? sectionCode, string trackCode) =>
            Task.FromResult(NavDummy());

        public Task<TrackNavigationResult> GetPreviousBiblePublicationTrack(string languageCode,
            string publicationCode, string? sectionCode, string trackCode) =>
            Task.FromResult(NavDummy());

        public Task<KeyValuePair<string, BiblePublicationSection>> GetPreviousBiblePublicationSection(string languageCode,
            string publicationCode, string sectionCode) =>
            Task.FromResult(default(KeyValuePair<string, BiblePublicationSection>));

        public Task<KeyValuePair<string, BiblePublicationSection>> GetNextBiblePublicationSection(string languageCode,
            string publicationCode, string sectionCode) =>
            Task.FromResult(default(KeyValuePair<string, BiblePublicationSection>));

        public Task<bool> ShouldResumeFromLastPositionAsync(int scheduleId) => Task.FromResult(false);

        public Task<TimeSpan> GetScheduleFinishedDurationAsync(int scheduleId) => Task.FromResult(TimeSpan.Zero);

        public Task<PlayItem> GetNextPlayItemAsync(TrackMetadata currentTrackMetadata,
            IFetchProgress? sectionFetchProgress = null) =>
            Task.FromResult(new PlayItem(St(), "u"));

        public Task<PlayItem> GetPreviousPlayItemAsync(TrackMetadata currentTrackMetadata,
            IFetchProgress? sectionFetchProgress = null) =>
            Task.FromResult(new PlayItem(St(), "u"));

        public Task PersistSchedulePointerToFinishedTrackAsync(TrackMetadata trackMetadata) =>
            Task.CompletedTask;

        private static TrackMetadata St() =>
            new()
            {
                LookUpPath = "stub-path",
                IsBibleContent = true,
                PublicationCode = "nwt",
                TrackCode = "1",
                ScheduleId = 1,
            };
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
            bool includeMusic = true, bool includeBiblePublication = true,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AlarmSchedule>());

        public Task<AlarmSchedule?> GetScheduleByIdAsync(int scheduleId, bool includeMusic,
            bool includeBiblePublication, CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmSchedule?>(null);

        public Task<AlarmSchedule?> GetFirstScheduleOrDefaultAsync(bool includeMusic,
            bool includeBiblePublication, CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmSchedule?>(null);

        public Task<AlarmSchedule> AddScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(schedule);

        public Task<AlarmSchedule> UpdateScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(schedule);

        public Task<AlarmSchedule> UpdateScheduleByIdAsync(int scheduleId,
            Action<AlarmSchedule> updateAction, CancellationToken cancellationToken = default) =>
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

    private sealed class ConfigurableStorage : IStorageService
    {
        internal Func<string, Task<bool>>? FileExistsImpl { get; init; }

        public string StorageRoot => @"C:\alarm-media-cache-tests";

        public string CacheRoot => "cache-root";

        public Task<bool> DirectoryExists(string path) => Task.FromResult(Directory.Exists(path));

        public Task<bool> FileExists(string path) =>
            FileExistsImpl?.Invoke(path) ?? Task.FromResult(false);

        public Task<List<string>> GetAllFiles(string path) => Task.FromResult(new List<string>());

        public Task<DateTimeOffset> GetFileCreationDate(string path) =>
            Task.FromResult(DateTimeOffset.UtcNow);

        [RequiresAssemblyFiles]
        public Task<DateTimeOffset> GetFileCreationDateFromResource(string resourceName) =>
            Task.FromResult(DateTimeOffset.UtcNow);

        public Task<string> ReadFile(string path) => Task.FromResult(string.Empty);

        public Task CopyResourceFile(string resourceFileName, string destinationDirectoryPath,
            string destinationFileName) =>
            Task.CompletedTask;

        public Task SaveFile(string directoryPath, string fileName, string contents) => Task.CompletedTask;

        public Task SaveFile(string directoryPath, string fileName, byte[] contents) => Task.CompletedTask;

        public Task DeleteFile(string path) => Task.CompletedTask;

        public Task DeleteDirectory(string path) => Task.CompletedTask;

        public Task<DirectoryInfo> CreateDirectory(string path) =>
            Task.FromResult(Directory.CreateDirectory(path));

        public void Dispose()
        {
        }
    }

    private sealed class OnlineNetwork : INetworkStatusService
    {
        public Task<bool> IsInternetAvailable() => Task.FromResult(true);
    }

    private sealed class RecordingDownload : IDownloadService
    {
        internal int DownloadCallCount { get; private set; }

        internal Func<int, Task<byte[]>>? DownloadImpl { get; init; }

        public void Dispose()
        {
        }

        public Task<byte[]> DownloadAsync(string url, string? alternativeUrl = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Array.Empty<byte>());

        public Task<byte[]> DownloadWithProgressAsync(string url,
            Action<long, long?>? progressCallback,
            CancellationToken cancellationToken = default)
        {
            DownloadCallCount++;
            var impl = DownloadImpl;
            return impl != null
                ? impl(DownloadCallCount)
                : Task.FromResult(new byte[] { 1, 2, 3 });
        }

        public Task<long?> GetContentLengthAsync(string url, CancellationToken cancellationToken = default) =>
            Task.FromResult<long?>(null);

        public Task<bool> FileExists(string url) => Task.FromResult(false);
    }

    private sealed class RefreshingTrackCdnUrl : ITrackCdnUrlRefresher
    {
        internal string? RefreshedUrl { get; init; }

        public Task<string?> TryRefreshTrackCdnUrlFromApiAsync(TrackMetadata metadata,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(RefreshedUrl);
    }

    private sealed class PlaylistReturningItems : IPlaylistService
    {
        private readonly List<PlayItem> items;

        public PlaylistReturningItems(List<PlayItem> items) => this.items = items;

        public void Dispose()
        {
        }

        private static TrackNavigationResult NavDummy() =>
            new("pub", null, new BiblePublicationTrack { TrackCode = "1" });

        public Task MarkTrackAsPlayed(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task MarkTrackAsFinished(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task<PlayItem> NextTrack(int scheduleId) =>
            Task.FromResult(items[0]);

        public Task<PlayItem?> NextBiblePublicationTrack(int scheduleId) =>
            Task.FromResult<PlayItem?>(null);

        public Task<List<PlayItem>> NextTracks(int scheduleId) => Task.FromResult(items);

        public Task SaveLastPlayed(int currentScheduleId) => Task.CompletedTask;

        public Task<int> GetRelevantScheduleToPlay() => Task.FromResult(0);

        public Task MoveToNextBiblePublicationTrack(int scheduleId) => Task.CompletedTask;

        public Task MoveToPreviousBiblePublicationTrack(int scheduleId) => Task.CompletedTask;

        public Task<TrackNavigationResult> GetNextBiblePublicationTrack(string languageCode,
            string publicationCode, string? sectionCode, string trackCode) =>
            Task.FromResult(NavDummy());

        public Task<TrackNavigationResult> GetPreviousBiblePublicationTrack(string languageCode,
            string publicationCode, string? sectionCode, string trackCode) =>
            Task.FromResult(NavDummy());

        public Task<KeyValuePair<string, BiblePublicationSection>> GetPreviousBiblePublicationSection(string languageCode,
            string publicationCode, string sectionCode) =>
            Task.FromResult(default(KeyValuePair<string, BiblePublicationSection>));

        public Task<KeyValuePair<string, BiblePublicationSection>> GetNextBiblePublicationSection(string languageCode,
            string publicationCode, string sectionCode) =>
            Task.FromResult(default(KeyValuePair<string, BiblePublicationSection>));

        public Task<bool> ShouldResumeFromLastPositionAsync(int scheduleId) => Task.FromResult(false);

        public Task<TimeSpan> GetScheduleFinishedDurationAsync(int scheduleId) => Task.FromResult(TimeSpan.Zero);

        public Task<PlayItem> GetNextPlayItemAsync(TrackMetadata currentTrackMetadata,
            IFetchProgress? sectionFetchProgress = null) =>
            Task.FromResult(items[0]);

        public Task<PlayItem> GetPreviousPlayItemAsync(TrackMetadata currentTrackMetadata,
            IFetchProgress? sectionFetchProgress = null) =>
            Task.FromResult(items[0]);

        public Task PersistSchedulePointerToFinishedTrackAsync(TrackMetadata trackMetadata) =>
            Task.CompletedTask;
    }

    private static PlayItem CachePlayItem(string lookUpPath, int scheduleId, string url = "https://cdn.example/a.mp3") =>
        new(
            new TrackMetadata
            {
                LookUpPath = lookUpPath,
                PublicationCode = "nwt",
                TrackCode = "1",
                IsBibleContent = true,
                ScheduleId = scheduleId,
            },
            url);

    private static MediaCacheService CreateSut(
        IStorageService? storage = null,
        IDownloadService? download = null,
        IPlaylistService? playlist = null,
        INetworkStatusService? network = null,
        ITrackCdnUrlRefresher? trackRefresh = null) =>
        new(new MediaCacheServiceDeps(
            TestLogging.CreateLogger(),
            storage ?? new StorageStub(),
            download ?? new IdleDownload(),
            playlist ?? new IdlePlaylistService(),
            new IdleCatalogMediaService(),
            network ?? new IdleNetwork(),
            new IdleUrlRefresh(),
            new IdleAlarmScheduleService(),
            trackRefresh ?? new IdleTrackRefresh()));

    private static async Task RunOrSoftSkipMainThreadComAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException
                                   || (ex is InvalidOperationException ioe && ioe.Message.Contains("MainThread")))
        {
        }
    }

    [Fact]
    public async Task SetupAlarmCache_invalid_schedule_returns_false_without_download()
    {
        var sut = CreateSut();
        Assert.False(await sut.SetupAlarmCacheAsync(-1));
        Assert.False(await sut.SetupAlarmCacheAsync(0));
    }

    [Fact]
    public void GetCacheFileName_matches_file_naming_delegate()
    {
        var sut = CreateSut();
        var lp = "pub/track?x";
        Assert.Equal(MediaCacheFileNaming.GetCacheFileName(lp), sut.GetCacheFileName(lp));
    }

    [Fact]
    public void GetCacheFilePath_uses_schedule_subfolder_under_configured_media_cache_root()
    {
        var sut = CreateSut();
        var lp = "lookup/a";
        var path = sut.GetCacheFilePath(lp, 902);
        var fileName = sut.GetCacheFileName(lp);

        Assert.Contains($"{Path.DirectorySeparatorChar}902{Path.DirectorySeparatorChar}", path);
        Assert.Contains($"{Path.DirectorySeparatorChar}MediaCache{Path.DirectorySeparatorChar}", path);
        Assert.EndsWith(fileName, path);
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        var sut = CreateSut();
        sut.Dispose();
        sut.Dispose();
    }

    [Fact]
    public async Task ExistsAsync_delegates_to_storage_FileExists_for_cache_path()
    {
        var sut = CreateSut(new ConfigurableStorage
        {
            FileExistsImpl = path =>
                Task.FromResult(path.Contains($"{Path.DirectorySeparatorChar}42{Path.DirectorySeparatorChar}", StringComparison.Ordinal)),
        });

        Assert.True(await sut.ExistsAsync("pub/track", 42));
        Assert.False(await sut.ExistsAsync("pub/track", 99));
    }

    [Fact]
    public async Task ResolveTrackUriAsync_returns_null_when_schedule_id_invalid()
    {
        var sut = CreateSut();
        var item = CachePlayItem("lp", scheduleId: 0);

        Assert.Null(await sut.ResolveTrackUriAsync(item));
    }

    [Fact]
    public async Task ResolveTrackUriAsync_returns_file_uri_when_cached_on_windows_host()
    {
        await RunOrSoftSkipMainThreadComAsync(async () =>
        {
            var sut = CreateSut(new ConfigurableStorage
            {
                FileExistsImpl = _ => Task.FromResult(true),
            });
            var item = CachePlayItem("cached/track", 15);

            var uri = await sut.ResolveTrackUriAsync(item);

            Assert.NotNull(uri);
            Assert.StartsWith("file:", uri, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task ResolveTrackUriAsync_returns_cdn_url_when_not_cached_and_online()
    {
        var sut = CreateSut(network: new OnlineNetwork());
        var item = CachePlayItem("stream/me", 3, "https://cdn.example/stream.mp3");

        Assert.Equal("https://cdn.example/stream.mp3", await sut.ResolveTrackUriAsync(item));
    }

    [Fact]
    public async Task ResolveTrackUriAsync_returns_null_when_not_cached_and_offline()
    {
        var sut = CreateSut();
        var item = CachePlayItem("offline/track", 8);

        Assert.Null(await sut.ResolveTrackUriAsync(item));
    }

    [Fact]
    public async Task CacheTrackAsync_returns_true_when_file_already_cached()
    {
        var sut = CreateSut(new ConfigurableStorage
        {
            FileExistsImpl = _ => Task.FromResult(true),
        });

        Assert.True(await sut.CacheTrackAsync(CachePlayItem("hit", 1), 1));
    }

    [Fact]
    public async Task CacheTrackAsync_returns_false_when_offline_and_not_cached()
    {
        var sut = CreateSut();

        Assert.False(await sut.CacheTrackAsync(CachePlayItem("miss", 1), 1));
    }

    [Fact]
    public async Task CacheTrackAsync_downloads_when_online_and_not_cached()
    {
        var download = new RecordingDownload();
        var sut = CreateSut(new ConfigurableStorage(), download, network: new OnlineNetwork());
        var item = CachePlayItem("download/me", 5);

        Assert.True(await sut.CacheTrackAsync(item, 5));
        Assert.Equal(1, download.DownloadCallCount);
    }

    [Fact]
    public async Task SetupAlarmCacheAsync_returns_true_when_playlist_item_requires_download()
    {
        var download = new RecordingDownload();
        var playlist = new PlaylistReturningItems([CachePlayItem("alarm/track", 77)]);
        var sut = CreateSut(new ConfigurableStorage(), download, playlist, new OnlineNetwork());

        Assert.True(await sut.SetupAlarmCacheAsync(77));
        Assert.True(download.DownloadCallCount >= 1);
    }

    [Fact]
    public async Task SetupAlarmCacheAsync_returns_false_when_playlist_empty()
    {
        var sut = CreateSut(playlist: new PlaylistReturningItems([]));

        Assert.False(await sut.SetupAlarmCacheAsync(12));
    }

    [Fact]
    public async Task CacheTrackAsync_refetches_cdn_url_after_404_and_retries_download()
    {
        var download = new RecordingDownload
        {
            DownloadImpl = call =>
                call == 1
                    ? throw new HttpRequestException("404 Not Found", null, HttpStatusCode.NotFound)
                    : Task.FromResult(new byte[] { 7 }),
        };
        var refresh = new RefreshingTrackCdnUrl { RefreshedUrl = "https://cdn.example/refreshed.mp3" };
        var sut = CreateSut(new ConfigurableStorage(), download, network: new OnlineNetwork(), trackRefresh: refresh);
        var item = CachePlayItem("refetch/track", 9, "https://cdn.example/stale.mp3");

        Assert.True(await sut.CacheTrackAsync(item, 9));
        Assert.Equal(2, download.DownloadCallCount);
        Assert.Equal("https://cdn.example/refreshed.mp3", item.Url);
    }

    [Fact]
    public async Task CleanUpAsync_completes_with_idle_dependencies()
    {
        var sut = CreateSut();
        await sut.CleanUpAsync();
    }

    [Fact]
    public async Task DeleteScheduleCacheAsync_completes_with_idle_dependencies()
    {
        var sut = CreateSut();
        await sut.DeleteScheduleCacheAsync(101);
    }
}
