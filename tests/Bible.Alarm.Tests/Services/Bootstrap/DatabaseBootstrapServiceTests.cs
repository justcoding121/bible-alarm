#nullable enable

using System.Diagnostics.CodeAnalysis;
using Bible.Alarm.Services.Bootstrap;
using Bible.Alarm.Services.Bootstrap.Interfaces;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Tests;

public sealed class DatabaseBootstrapServiceTests
{
    private sealed class StubScheduleDatabaseVersionService : IScheduleDatabaseVersionService
    {
        public Task<bool> IsVersionCurrentAsync() => Task.FromResult(true);

        public Task SaveCurrentVersionAsync() => Task.CompletedTask;
    }

    private sealed class TempDirStorage : IStorageService
    {
        private readonly string root;

        public TempDirStorage(string root) => this.root = root;

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
        var sut = new DatabaseBootstrapService(
            null!,
            null!,
            null!);

        Assert.NotNull(sut);
    }

    [Fact]
    public async Task InitializeAsync_completes_when_migrated_database_exists_and_version_matches()
    {
        var tempDir = Directory.CreateTempSubdirectory("bib-alarm-db-bootstrap").FullName;
        var dbPath = Path.Combine(tempDir, AppConstants.Database.ScheduleDatabaseFileName);
        var connectionString = string.Format(AppConstants.Database.ScheduleDatabaseConnectionStringFormat, dbPath);

        try
        {
            var services = new ServiceCollection();
            services.AddDbContext<ScheduleDbContext>(o => o.UseSqlite(connectionString));
            services.AddSingleton<IScheduleDatabaseVersionService, StubScheduleDatabaseVersionService>();
            services.AddSingleton<IStorageService>(_ => new TempDirStorage(tempDir));
            services.AddSingleton<IDatabaseBootstrapService>(sp => new DatabaseBootstrapService(
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<IScheduleDatabaseVersionService>(),
                sp.GetRequiredService<IStorageService>()));

            await using var provider = services.BuildServiceProvider();

            using (var scope = provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
                await db.Database.MigrateAsync();
            }

            var sut = provider.GetRequiredService<IDatabaseBootstrapService>();
            await sut.InitializeAsync();

            Assert.True(File.Exists(dbPath));
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // best-effort cleanup on locked SQLite files
            }
        }
    }
}
