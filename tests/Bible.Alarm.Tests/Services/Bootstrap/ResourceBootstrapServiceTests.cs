#nullable enable

using System.Diagnostics.CodeAnalysis;
using Bible.Alarm.Services.Bootstrap;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;

namespace Bible.Alarm.Tests;

public sealed class ResourceBootstrapServiceTests
{
    private sealed class RecordingMediaIndex : IMediaIndexService
    {
        public int VerifyCallCount { get; private set; }

        public int MigrateCallCount { get; private set; }

        public string IndexRoot => "index-root";

        public bool WasIndexReplacedThisRun { get; set; }

        public void Dispose()
        {
        }

        public Task MigrateNonEnglishDataIfNeededAsync()
        {
            MigrateCallCount++;
            return Task.CompletedTask;
        }

        public Task Verify()
        {
            VerifyCallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class NopStorage : IStorageService
    {
        public string StorageRoot => Path.Combine(Path.GetTempPath(), "resource-bootstrap-tests");

        public string CacheRoot => Path.Combine(Path.GetTempPath(), "resource-bootstrap-cache");

        public Task<bool> DirectoryExists(string path) => Task.FromResult(false);

        public Task<bool> FileExists(string path) => Task.FromResult(false);

        public Task<List<string>> GetAllFiles(string path) => Task.FromResult(new List<string>());

        public Task<DateTimeOffset> GetFileCreationDate(string path) =>
            Task.FromResult(DateTimeOffset.UtcNow);

        [RequiresAssemblyFiles]
        public Task<DateTimeOffset> GetFileCreationDateFromResource(string resourceName) =>
            Task.FromResult(DateTimeOffset.UtcNow);

        public Task<string> ReadFile(string path) => Task.FromResult(string.Empty);

        public Task CopyResourceFile(string resourceFileName, string destinationDirectoryPath, string destinationFileName) =>
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

    [Fact]
    public async Task CopyResourcesAsync_calls_Verify_before_completing()
    {
        var media = new RecordingMediaIndex();
        var sut = new ResourceBootstrapService(media, new NopStorage());

        await sut.CopyResourcesAsync();

        Assert.Equal(1, media.VerifyCallCount);
    }

    [Fact]
    public void WasMediaIndexReplacedThisRun_reflects_media_index_service()
    {
        var media = new RecordingMediaIndex { WasIndexReplacedThisRun = true };
        var sut = new ResourceBootstrapService(media, new NopStorage());

        Assert.True(sut.WasMediaIndexReplacedThisRun());
    }

    [Fact]
    public async Task MigrateNonEnglishMediaDataAsync_delegates_to_media_index_service()
    {
        var media = new RecordingMediaIndex();
        var sut = new ResourceBootstrapService(media, new NopStorage());

        await sut.MigrateNonEnglishMediaDataAsync();

        Assert.Equal(1, media.MigrateCallCount);
    }
}
