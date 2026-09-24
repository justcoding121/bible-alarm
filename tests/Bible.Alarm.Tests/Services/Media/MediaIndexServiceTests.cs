#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Tests;

public sealed class MediaIndexServiceTests
{
    private sealed class StubMediaIndexVersionService : IMediaIndexVersionService
    {
        internal bool IsCurrent { get; init; } = true;

        public Task<bool> VersionFileExistsAsync() => Task.FromResult(true);

        public Task<string?> ReadVersionAsync() => Task.FromResult<string?>("1");

        public Task<bool> IsVersionCurrentAsync() => Task.FromResult(IsCurrent);

        public Task SaveCurrentVersionAsync() => Task.CompletedTask;
    }

    private sealed class IdleLanguageContentService : ILanguageContentService
    {
        public Task<string?> GetVideoPublicationDisplayNameAsync(string publicationCode, string languageCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<bool> FetchPublicationTracksAsync(string publicationCode, string languageCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> FetchPublicationSectionsAsync(string publicationCode, string languageCode,
            IFetchProgress? progress = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> FetchSectionTracksAsync(string publicationCode, string sectionCode, string languageCode,
            bool replaceExistingTracksFromApi = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> SeedEnglishPublicationAsync(string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> EnsurePublicationExistsAsync(string publicationCode, string languageCode,
            IFetchProgress? progress = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> FetchFirstPublicationForLanguageAsync(string languageCode, string? categoryName = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> EnsureAllPublicationsForLanguageAsync(string languageCode, string? categoryName = null,
            IFetchProgress? progress = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> EnsureAllSectionsForPublicationAsync(string publicationCode, string languageCode,
            IFetchProgress? progress = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> FetchFirstSectionOnlyAsync(string publicationCode, string firstSectionCode, string languageCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class FileSystemIndexStorage : IStorageService
    {
        private readonly string root;

        public FileSystemIndexStorage(string root) => this.root = root;

        public string StorageRoot => root;

        public string CacheRoot => Path.Combine(root, "cache");

        public Task<bool> DirectoryExists(string path) => Task.FromResult(Directory.Exists(path));

        public Task<bool> FileExists(string path) => Task.FromResult(File.Exists(path));

        public Task<List<string>> GetAllFiles(string path) => Task.FromResult(new List<string>());

        public Task<DateTimeOffset> GetFileCreationDate(string path) =>
            Task.FromResult(DateTimeOffset.UtcNow);

        [RequiresAssemblyFiles]
        public Task<DateTimeOffset> GetFileCreationDateFromResource(string resourceName) =>
            Task.FromResult(DateTimeOffset.UtcNow);

        public Task<string> ReadFile(string path) => Task.FromResult(string.Empty);

        public Task CopyResourceFile(string resourceFileName, string destinationDirectoryPath, string destinationFileName)
        {
            var zipPath = Path.Combine(destinationDirectoryPath, destinationFileName);
            WriteMinimalIndexZip(zipPath, AppConstants.Database.MediaIndexDatabaseFileName);
            return Task.CompletedTask;
        }

        public Task SaveFile(string directoryPath, string fileName, string contents) => Task.CompletedTask;

        public Task SaveFile(string directoryPath, string fileName, byte[] contents) => Task.CompletedTask;

        public Task DeleteFile(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return Task.CompletedTask;
        }

        public Task DeleteDirectory(string path) => Task.CompletedTask;

        public Task<DirectoryInfo> CreateDirectory(string path) =>
            Task.FromResult(Directory.CreateDirectory(path));

        public void Dispose()
        {
        }
    }

    private static void WriteMinimalIndexZip(string zipPath, string entryName)
    {
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        using var stream = entry.Open();
        stream.Write(new byte[] { 0x53, 0x51, 0x4C, 0x69, 0x74, 0x65, 0x20, 0x66, 0x6F, 0x72, 0x6D, 0x61, 0x74, 0x20, 0x33, 0x00 }, 0, 16);
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;
        using (var init = new MediaDbContext(options))
        {
            init.Database.EnsureCreated();
        }

        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddScoped<MediaDbContext>(_ => new MediaDbContext(options));
        return services.BuildServiceProvider();
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

    [Fact]
    public async Task Verify_second_call_is_idempotent_when_index_current()
    {
        MediaIndexService.ResetVerificationStateForTests();
        var root = Directory.CreateTempSubdirectory("media-index-idempotent").FullName;
        try
        {
            var sut = new MediaIndexService(
                TestLogging.CreateLogger(),
                new CurrentIndexStorage(root),
                new StubMediaIndexVersionService(),
                BuildServiceProvider(),
                new IdleLanguageContentService());

            await sut.Verify();
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

    [Fact]
    public async Task Verify_when_version_outdated_replaces_index_and_sets_WasIndexReplacedThisRun()
    {
        MediaIndexService.ResetVerificationStateForTests();
        var root = Directory.CreateTempSubdirectory("media-index-replace").FullName;
        try
        {
            Directory.CreateDirectory(root);
            var existingDb = Path.Combine(root, AppConstants.Database.MediaIndexDatabaseFileName);
            await File.WriteAllBytesAsync(existingDb, [1, 2, 3]);

            var sut = new MediaIndexService(
                TestLogging.CreateLogger(),
                new FileSystemIndexStorage(root),
                new StubMediaIndexVersionService { IsCurrent = false },
                BuildServiceProvider(),
                new IdleLanguageContentService());

            await sut.Verify();

            Assert.True(sut.WasIndexReplacedThisRun);
            Assert.True(File.Exists(existingDb));
            var oldPath = Path.Combine(root,
                Path.GetFileNameWithoutExtension(AppConstants.Database.MediaIndexDatabaseFileName)
                + AppConstants.Database.MediaIndexDatabaseRenamedSuffix
                + Path.GetExtension(AppConstants.Database.MediaIndexDatabaseFileName));
            Assert.True(File.Exists(oldPath));
            Assert.False(File.Exists(Path.Combine(root, AppConstants.FilePaths.MediaIndexZipFileName)));
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

    [Fact]
    public async Task MigrateNonEnglishDataIfNeededAsync_no_op_when_old_index_missing()
    {
        MediaIndexService.ResetVerificationStateForTests();
        var root = Directory.CreateTempSubdirectory("media-index-migrate-skip").FullName;
        try
        {
            var sut = new MediaIndexService(
                TestLogging.CreateLogger(),
                new FileSystemIndexStorage(root),
                new StubMediaIndexVersionService(),
                BuildServiceProvider(),
                new IdleLanguageContentService());

            await sut.MigrateNonEnglishDataIfNeededAsync();
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

    [Fact]
    public void IndexRoot_returns_storage_root_from_lazy_initializer()
    {
        var root = Directory.CreateTempSubdirectory("media-index-root").FullName;
        try
        {
            var sut = new MediaIndexService(
                TestLogging.CreateLogger(),
                new FileSystemIndexStorage(root),
                new StubMediaIndexVersionService(),
                null!,
                null!);

            Assert.Equal(root, sut.IndexRoot);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        var sut = new MediaIndexService(
            TestLogging.CreateLogger(),
            new CurrentIndexStorage(Directory.CreateTempSubdirectory("media-index-dispose").FullName),
            new StubMediaIndexVersionService(),
            null!,
            null!);

        sut.Dispose();
        sut.Dispose();
    }
}
