#nullable enable

using System.Diagnostics.CodeAnalysis;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class MediaIndexServiceTests
{
    private sealed class StubMediaIndexVersionService : IMediaIndexVersionService
    {
        public Task<bool> VersionFileExistsAsync() => Task.FromResult(true);

        public Task<string?> ReadVersionAsync() => Task.FromResult<string?>("1");

        public Task<bool> IsVersionCurrentAsync() => Task.FromResult(true);

        public Task SaveCurrentVersionAsync() => Task.CompletedTask;
    }

    private sealed class CurrentIndexStorage : IStorageService
    {
        private readonly string root;

        public CurrentIndexStorage(string root) => this.root = root;

        public string StorageRoot => root;

        public string CacheRoot => Path.Combine(root, "cache");

        public Task<bool> DirectoryExists(string path) => Task.FromResult(Directory.Exists(path));

        public Task<bool> FileExists(string path)
        {
            var name = Path.GetFileName(path);
            if (string.Equals(name, AppConstants.Database.MediaIndexDatabaseFileName, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(true);
            }

            if (string.Equals(name, AppConstants.FilePaths.MediaIndexZipFileName, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(false);
        }

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
            Task.FromResult(Directory.CreateDirectory(path));

        public void Dispose()
        {
        }
    }

    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var sut = new MediaIndexService(
            TestLogging.CreateLogger(),
            null!,
            null!,
            null!,
            null!);

        Assert.NotNull(sut);
    }

    [Fact]
    public async Task Verify_when_index_is_current_does_not_mark_replaced()
    {
        MediaIndexService.ResetVerificationStateForTests();
        var root = Directory.CreateTempSubdirectory("media-index-verify").FullName;
        try
        {
            var sut = new MediaIndexService(
                TestLogging.CreateLogger(),
                new CurrentIndexStorage(root),
                new StubMediaIndexVersionService(),
                null!,
                null!);

            await sut.Verify();

            Assert.False(sut.WasIndexReplacedThisRun);
        }
        finally
        {
            MediaIndexService.ResetVerificationStateForTests();
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }
}
