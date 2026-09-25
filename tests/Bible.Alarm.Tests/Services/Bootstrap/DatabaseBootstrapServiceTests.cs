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

    private sealed class OutdatedScheduleDatabaseVersionService : IScheduleDatabaseVersionService
    {
        public int SaveCalls { get; private set; }

        public Task<bool> IsVersionCurrentAsync() => Task.FromResult(false);

        public Task SaveCurrentVersionAsync()
        {
            SaveCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class TempDirStorage : IStorageService
    {
        private readonly string root;
        private readonly string? bundledSourcePath;
        private readonly bool throwOnCopy;

        public TempDirStorage(string root, string? bundledSourcePath = null, bool throwOnCopy = false)
        {
            this.root = root;
            this.bundledSourcePath = bundledSourcePath;
            this.throwOnCopy = throwOnCopy;
        }

        public int CopyResourceFileCalls { get; private set; }

        public string? LastCopiedDestinationPath { get; private set; }

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
            CopyResourceFileCalls++;
            LastCopiedDestinationPath = Path.Combine(destinationDirectoryPath, destinationFileName);

            if (throwOnCopy)
            {
                throw new FileNotFoundException("Simulated missing bundled schedule database resource");
            }

            if (bundledSourcePath != null)
            {
                Directory.CreateDirectory(destinationDirectoryPath);
                File.Copy(bundledSourcePath, LastCopiedDestinationPath, overwrite: true);
            }

            return Task.CompletedTask;
        }

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

    private static async Task<(ServiceProvider Provider, string TempDir, string DbPath, TempDirStorage Storage)> BuildProviderAsync(
        IScheduleDatabaseVersionService versionService,
        TempDirStorage storage,
        string tempDirPrefix)
    {
        var tempDir = Directory.CreateTempSubdirectory(tempDirPrefix).FullName;
        var dbPath = Path.Combine(tempDir, AppConstants.Database.ScheduleDatabaseFileName);
        var connectionString = string.Format(AppConstants.Database.ScheduleDatabaseConnectionStringFormat, dbPath);

        var services = new ServiceCollection();
        services.AddDbContext<ScheduleDbContext>(o => o.UseSqlite(connectionString));
        services.AddSingleton(versionService);
        services.AddSingleton<IStorageService>(_ => storage);
        services.AddSingleton<IDatabaseBootstrapService>(sp => new DatabaseBootstrapService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<IScheduleDatabaseVersionService>(),
            sp.GetRequiredService<IStorageService>()));

        var provider = services.BuildServiceProvider();
        return (provider, tempDir, dbPath, storage);
    }

    private static async Task CreateMigratedScheduleDbFileAsync(string dbPath)
    {
        var connectionString = string.Format(AppConstants.Database.ScheduleDatabaseConnectionStringFormat, dbPath);
        var options = new DbContextOptionsBuilder<ScheduleDbContext>()
            .UseSqlite(connectionString)
            .Options;

        await using var db = new ScheduleDbContext(options);
        await db.Database.MigrateAsync();
    }

    private static void TryDeleteTempDir(string tempDir)
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
            TryDeleteTempDir(tempDir);
        }
    }

    [Fact]
    public async Task InitializeAsync_saves_version_when_stored_version_is_outdated()
    {
        var tempDir = Directory.CreateTempSubdirectory("bib-alarm-db-bootstrap-ver").FullName;
        var dbPath = Path.Combine(tempDir, AppConstants.Database.ScheduleDatabaseFileName);
        var connectionString = string.Format(AppConstants.Database.ScheduleDatabaseConnectionStringFormat, dbPath);

        try
        {
            var versionService = new OutdatedScheduleDatabaseVersionService();
            var services = new ServiceCollection();
            services.AddDbContext<ScheduleDbContext>(o => o.UseSqlite(connectionString));
            services.AddSingleton<IScheduleDatabaseVersionService>(versionService);
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

            Assert.Equal(1, versionService.SaveCalls);
        }
        finally
        {
            TryDeleteTempDir(tempDir);
        }
    }

    [Fact]
    public async Task InitializeAsync_deletes_legacy_schedule_database_files()
    {
        var storage = new TempDirStorage(Directory.CreateTempSubdirectory("bib-alarm-db-legacy-root").FullName);
        var (provider, tempDir, dbPath, _) = await BuildProviderAsync(
            new StubScheduleDatabaseVersionService(),
            storage,
            "bib-alarm-db-legacy");

        try
        {
            await using (provider)
            {
                using (var scope = provider.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
                    await db.Database.MigrateAsync();
                }

                var legacy1 = Path.Combine(tempDir, AppConstants.Database.ScheduleDatabaseLegacyBibleAlarmFileName);
                var legacy2 = Path.Combine(tempDir, AppConstants.Database.ScheduleDatabaseLegacyBibleAlarm2FileName);
                await File.WriteAllTextAsync(legacy1, "legacy-1");
                await File.WriteAllTextAsync(legacy2, "legacy-2");
                await File.WriteAllTextAsync(legacy1 + AppConstants.Database.SqliteWalFileSuffix, "wal");
                await File.WriteAllTextAsync(legacy1 + AppConstants.Database.SqliteShmFileSuffix, "shm");

                var sut = provider.GetRequiredService<IDatabaseBootstrapService>();
                await sut.InitializeAsync();

                Assert.False(File.Exists(legacy1));
                Assert.False(File.Exists(legacy2));
                Assert.False(File.Exists(legacy1 + AppConstants.Database.SqliteWalFileSuffix));
                Assert.False(File.Exists(legacy1 + AppConstants.Database.SqliteShmFileSuffix));
                Assert.True(File.Exists(dbPath));
            }
        }
        finally
        {
            TryDeleteTempDir(tempDir);
            TryDeleteTempDir(storage.StorageRoot);
        }
    }

    [Fact]
    public async Task InitializeAsync_copies_bundled_resource_when_schedule_db_is_missing()
    {
        var bundledDir = Directory.CreateTempSubdirectory("bib-alarm-db-bundled-src").FullName;
        var bundledDbPath = Path.Combine(bundledDir, "bundled-schedule.db");
        await CreateMigratedScheduleDbFileAsync(bundledDbPath);

        var storage = new TempDirStorage(
            Directory.CreateTempSubdirectory("bib-alarm-db-copy-root").FullName,
            bundledSourcePath: bundledDbPath);
        var (provider, tempDir, dbPath, trackedStorage) = await BuildProviderAsync(
            new OutdatedScheduleDatabaseVersionService(),
            storage,
            "bib-alarm-db-copy");

        try
        {
            await using (provider)
            {
                Assert.False(File.Exists(dbPath));

                var sut = provider.GetRequiredService<IDatabaseBootstrapService>();
                await sut.InitializeAsync();

                Assert.Equal(1, trackedStorage.CopyResourceFileCalls);
                Assert.True(File.Exists(dbPath));
            }
        }
        finally
        {
            TryDeleteTempDir(tempDir);
            TryDeleteTempDir(storage.StorageRoot);
            TryDeleteTempDir(bundledDir);
        }
    }

    [Fact]
    public async Task InitializeAsync_falls_back_to_migrations_when_resource_copy_fails()
    {
        var storage = new TempDirStorage(
            Directory.CreateTempSubdirectory("bib-alarm-db-copy-fail-root").FullName,
            throwOnCopy: true);
        var versionService = new OutdatedScheduleDatabaseVersionService();
        var (provider, tempDir, dbPath, trackedStorage) = await BuildProviderAsync(
            versionService,
            storage,
            "bib-alarm-db-copy-fail");

        try
        {
            await using (provider)
            {
                var sut = provider.GetRequiredService<IDatabaseBootstrapService>();
                await sut.InitializeAsync();

                Assert.Equal(1, trackedStorage.CopyResourceFileCalls);
                Assert.True(File.Exists(dbPath));
                Assert.Equal(1, versionService.SaveCalls);
            }
        }
        finally
        {
            TryDeleteTempDir(tempDir);
            TryDeleteTempDir(storage.StorageRoot);
        }
    }

    [Fact]
    public async Task InitializeAsync_recovers_by_replacing_corrupt_schedule_database()
    {
        var bundledDir = Directory.CreateTempSubdirectory("bib-alarm-db-recover-src").FullName;
        var bundledDbPath = Path.Combine(bundledDir, "bundled-schedule.db");
        await CreateMigratedScheduleDbFileAsync(bundledDbPath);

        var storage = new TempDirStorage(
            Directory.CreateTempSubdirectory("bib-alarm-db-recover-root").FullName,
            bundledSourcePath: bundledDbPath);
        var versionService = new OutdatedScheduleDatabaseVersionService();
        var (provider, tempDir, dbPath, trackedStorage) = await BuildProviderAsync(
            versionService,
            storage,
            "bib-alarm-db-recover");

        try
        {
            await using (provider)
            {
                await File.WriteAllTextAsync(dbPath, "not-a-sqlite-database");
                await File.WriteAllTextAsync(dbPath + AppConstants.Database.SqliteWalFileSuffix, "wal");
                await File.WriteAllTextAsync(dbPath + AppConstants.Database.SqliteShmFileSuffix, "shm");

                var sut = provider.GetRequiredService<IDatabaseBootstrapService>();
                await sut.InitializeAsync();

                Assert.True(trackedStorage.CopyResourceFileCalls >= 1);
                Assert.True(File.Exists(dbPath));
                Assert.Equal(1, versionService.SaveCalls);

                await using var verify = new ScheduleDbContext(
                    new DbContextOptionsBuilder<ScheduleDbContext>()
                        .UseSqlite(string.Format(AppConstants.Database.ScheduleDatabaseConnectionStringFormat, dbPath))
                        .Options);
                Assert.Empty(await verify.Database.GetPendingMigrationsAsync());
            }
        }
        finally
        {
            TryDeleteTempDir(tempDir);
            TryDeleteTempDir(storage.StorageRoot);
            TryDeleteTempDir(bundledDir);
        }
    }

    [Fact]
    public async Task InitializeAsync_does_not_save_version_when_version_already_current()
    {
        var spy = new SpyCurrentVersionService();
        var storage = new TempDirStorage(Directory.CreateTempSubdirectory("bib-alarm-db-nosave-root").FullName);
        var (provider, tempDir, _, _) = await BuildProviderAsync(spy, storage, "bib-alarm-db-nosave");

        try
        {
            await using (provider)
            {
                using (var scope = provider.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
                    await db.Database.MigrateAsync();
                }

                var sut = provider.GetRequiredService<IDatabaseBootstrapService>();
                await sut.InitializeAsync();

                Assert.Equal(0, spy.SaveCalls);
            }
        }
        finally
        {
            TryDeleteTempDir(tempDir);
            TryDeleteTempDir(storage.StorageRoot);
        }
    }

    private sealed class SpyCurrentVersionService : IScheduleDatabaseVersionService
    {
        public int SaveCalls { get; private set; }

        public Task<bool> IsVersionCurrentAsync() => Task.FromResult(true);

        public Task SaveCurrentVersionAsync()
        {
            SaveCalls++;
            return Task.CompletedTask;
        }
    }
}
