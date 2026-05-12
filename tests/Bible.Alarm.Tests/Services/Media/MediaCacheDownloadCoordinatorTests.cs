#nullable enable

using System.Collections.Concurrent;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Tests.Support;
using Serilog;

namespace Bible.Alarm.Tests;

public sealed class MediaCacheDownloadCoordinatorTests
{
    private sealed class RecordingDownloadService : IDownloadService
    {
        public Func<string, Action<long, long?>?, CancellationToken, Task<byte[]>>? DownloadImpl { get; set; }

        public int DownloadCallCount { get; private set; }

        public Task<byte[]> DownloadAsync(string url, string? alternativeUrl = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<byte[]> DownloadWithProgressAsync(string url,
            Action<long, long?>? progressCallback,
            CancellationToken cancellationToken = default)
        {
            DownloadCallCount++;
            return DownloadImpl != null
                ? DownloadImpl(url, progressCallback, cancellationToken)
                : Task.FromResult(Array.Empty<byte>());
        }

        public Task<long?> GetContentLengthAsync(string url, CancellationToken cancellationToken = default) =>
            Task.FromResult<long?>(null);

        public Task<bool> FileExists(string url) => Task.FromResult(false);

        public void Dispose()
        {
        }
    }

    private sealed class RecordingStorageService : IStorageService
    {
        public List<(string Dir, string FileName, byte[] Bytes)> Saves { get; } = [];

        public string StorageRoot => "stor";
        public string CacheRoot => "cache";

        public Task<bool> DirectoryExists(string path) => Task.FromResult(true);

        public Task<bool> FileExists(string path) => Task.FromResult(false);

        public Task<List<string>> GetAllFiles(string path) => Task.FromResult(new List<string>());

        public Task<DateTimeOffset> GetFileCreationDate(string path) =>
            Task.FromResult(DateTimeOffset.UtcNow);

        public Task<DateTimeOffset> GetFileCreationDateFromResource(string resourceName) =>
            Task.FromResult(DateTimeOffset.UtcNow);

        public Task<string> ReadFile(string path) => Task.FromResult("");

        public Task CopyResourceFile(string resourceFileName, string destinationDirectoryPath,
            string destinationFileName) =>
            Task.CompletedTask;

        public Task SaveFile(string directoryPath, string fileName, string contents) => Task.CompletedTask;

        public Task SaveFile(string directoryPath, string fileName, byte[] contents)
        {
            Saves.Add((directoryPath, fileName, contents));
            return Task.CompletedTask;
        }

        public Task DeleteFile(string path) => Task.CompletedTask;

        public Task DeleteDirectory(string path) => Task.CompletedTask;

        public Task<DirectoryInfo> CreateDirectory(string path) =>
            Task.FromResult(new DirectoryInfo(path));

        public void Dispose()
        {
        }
    }

    private sealed class NullUrlRefresh : IMediaUrlRefreshService
    {
        public static readonly NullUrlRefresh Instance = new();

        public Task<string?> RefreshUrlAsync(TrackMetadata trackMetadata) => Task.FromResult<string?>(null);
    }

    private static PlayItem Track(string lp, string url = "https://dl") =>
        new(
            new TrackMetadata
                {
                    LookUpPath = lp,
                    PublicationCode = "nwt",
                    TrackCode = "1",
                    IsBibleContent = true,
                    ScheduleId = 44,
                },
            url);

    private static DownloadAndCacheTrackWithProgressArgs Args(
        ILogger logger,
        IDownloadService download,
        IStorageService storage,
        ConcurrentDictionary<string, Task<string?>> inbox,
        PlayItem playItem,
        int scheduleId,
        CancellationToken ct = default) =>
        new(logger,
            download,
            storage,
            NullUrlRefresh.Instance,
            new IdleCatalogMediaService(),
            inbox,
            id => $"/sch/{id}",
            lp => $"file-{lp.Replace('/', '_')}.bin",
            playItem,
            scheduleId,
            ProgressCallback: null,
            ct);

    [Fact]
    public async Task DownloadAndCacheTrackWithProgressAsync_saves_and_returns_url_when_bytes_present()
    {
        var inbox = new ConcurrentDictionary<string, Task<string?>>();
        var dl = new RecordingDownloadService
        {
            DownloadImpl = (_, _, _) => Task.FromResult(new byte[] { 9, 8 }),
        };
        var store = new RecordingStorageService();
        var args = Args(TestLogging.CreateLogger(), dl, store, inbox, Track("pub/a"), 7);

        var result = await MediaCacheDownloadCoordinator.DownloadAndCacheTrackWithProgressAsync(args);

        Assert.Equal("https://dl", result);
        Assert.Single(store.Saves);
        Assert.Equal("/sch/7", store.Saves[0].Dir);
        Assert.Equal("file-pub_a.bin", store.Saves[0].FileName);
        Assert.Equal(new byte[] { 9, 8 }, store.Saves[0].Bytes);
        Assert.False(inbox.ContainsKey("7:pub/a")); // coordinator removes managed key after completion
    }

    [Fact]
    public async Task DownloadAndCacheTrackWithProgressAsync_returns_null_when_bytes_empty()
    {
        var inbox = new ConcurrentDictionary<string, Task<string?>>();
        var dl = new RecordingDownloadService
        {
            DownloadImpl = (_, _, _) => Task.FromResult(Array.Empty<byte>()),
        };
        var args = Args(TestLogging.CreateLogger(),
            dl,
            new RecordingStorageService(),
            inbox,
            Track("only"),
            1);

        Assert.Null(await MediaCacheDownloadCoordinator.DownloadAndCacheTrackWithProgressAsync(args));
        Assert.Equal(1, dl.DownloadCallCount);
    }

    [Fact]
    public async Task DownloadAndCacheTrackWithProgressAsync_existing_entry_is_awaited_without_download_side_effects_and_not_removed()
    {
        var inbox = new ConcurrentDictionary<string, Task<string?>>();
        inbox["88:reuse"] = Task.FromResult<string?>("done");
        var dl = new RecordingDownloadService
        {
            DownloadImpl = (_, _, _) =>
                throw new InvalidOperationException("should not download"),
        };
        var args = Args(TestLogging.CreateLogger(),
            dl,
            new RecordingStorageService(),
            inbox,
            Track("reuse", url: "u"),
            88);

        Assert.Equal("done", await MediaCacheDownloadCoordinator.DownloadAndCacheTrackWithProgressAsync(args));
        Assert.Equal(0, dl.DownloadCallCount);
        Assert.True(inbox.TryGetValue("88:reuse", out _));
    }

    [Fact]
    public async Task DownloadAndCacheTrackWithProgressAsync_already_cancelled_does_not_leak_managed_key_when_task_never_registered()
    {
        var inbox = new ConcurrentDictionary<string, Task<string?>>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var args = Args(TestLogging.CreateLogger(),
            new RecordingDownloadService(),
            new RecordingStorageService(),
            inbox,
            Track("x"),
            1,
            cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            MediaCacheDownloadCoordinator.DownloadAndCacheTrackWithProgressAsync(args));

        Assert.False(inbox.ContainsKey("1:x"));
    }
}
