#nullable enable

using Bible.Alarm.Common.Interfaces.Platform;
using Bible.Alarm.Common.Interfaces.Storage;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class MediaIndexVersionServiceTests
{
    private sealed class RecordingStorage : IStorageService
    {
        public RecordingStorage(Dictionary<string, string>? fileReads = null)
        {
            FileReadsByPath = fileReads ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public Dictionary<string, string> FileReadsByPath { get; }

        public List<(string Dir, string FileName, string Contents)> Saves { get; } = [];

        public string StorageRoot => @"Z:\media-root";

        public string CacheRoot => "cache";

        public Task<bool> DirectoryExists(string path) => Task.FromResult(false);

        public Task<bool> FileExists(string path) =>
            Task.FromResult(FileReadsByPath.Keys.Any(k => PathsEqual(path, k)));

        public Task<List<string>> GetAllFiles(string path) => Task.FromResult(new List<string>());

        public Task<DateTimeOffset> GetFileCreationDate(string path) =>
            Task.FromResult(DateTimeOffset.UtcNow);

        public Task<DateTimeOffset> GetFileCreationDateFromResource(string resourceName) =>
            Task.FromResult(DateTimeOffset.UtcNow);

        public Task<string> ReadFile(string path)
        {
            foreach (var kv in FileReadsByPath)
            {
                if (PathsEqual(kv.Key, path))
                {
                    return Task.FromResult(kv.Value);
                }
            }

            return Task.FromResult(string.Empty);
        }

        private static bool PathsEqual(string a, string b) =>
            string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

        public Task CopyResourceFile(string resourceFileName, string destinationDirectoryPath,
            string destinationFileName) =>
            Task.CompletedTask;

        public Task SaveFile(string directoryPath, string fileName, string contents) =>
            SaveFile(directoryPath, fileName, System.Text.Encoding.UTF8.GetBytes(contents));

        public Task SaveFile(string directoryPath, string fileName, byte[] contents)
        {
            Saves.Add((directoryPath, fileName, System.Text.Encoding.UTF8.GetString(contents)));
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

    private sealed class StubVersionFinder(string name) : IVersionFinder
    {
        public string GetVersionName() => name;
    }

    /// <summary>In-memory Preferences stub (parity with other service tests).</summary>
    private sealed class FakePrefs : IThreadSafePreferencesService
    {
        private readonly Dictionary<string, string> strings = [];

        public bool ContainsKey(string key, string? sharedName = null) => strings.ContainsKey(key);

        public string Get(string key, string defaultValue = "", string? sharedName = null) =>
            strings.TryGetValue(key, out var v) ? v : defaultValue;

        public void Set(string key, string value, string? sharedName = null) => strings[key] = value;

        public void Remove(string key, string? sharedName = null) => strings.Remove(key);

        public void Clear(string? sharedName = null) => strings.Clear();

        public int Get(string key, int defaultValue = 0, string? sharedName = null) => defaultValue;

        public bool Get(string key, bool defaultValue = false, string? sharedName = null) => defaultValue;

        public double Get(string key, double defaultValue = 0.0, string? sharedName = null) => defaultValue;

        public float Get(string key, float defaultValue = 0f, string? sharedName = null) => defaultValue;

        public long Get(string key, long defaultValue = 0L, string? sharedName = null) => defaultValue;

        public DateTime Get(string key, DateTime defaultValue, string? sharedName = null) => defaultValue;

        public void Set(string key, int value, string? sharedName = null)
        {
        }

        public void Set(string key, bool value, string? sharedName = null)
        {
        }

        public void Set(string key, double value, string? sharedName = null)
        {
        }

        public void Set(string key, float value, string? sharedName = null)
        {
        }

        public void Set(string key, long value, string? sharedName = null)
        {
        }

        public void Set(string key, DateTime value, string? sharedName = null)
        {
        }

        public Task<string> GetAsync(string key, string defaultValue = "", string? sharedName = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Get(key, defaultValue, sharedName));

        public Task<int> GetAsync(string key, int defaultValue = 0, string? sharedName = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Get(key, defaultValue, sharedName));

        public Task<bool> GetAsync(string key, bool defaultValue = false, string? sharedName = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Get(key, defaultValue, sharedName));

        public Task<double> GetAsync(string key, double defaultValue = 0.0, string? sharedName = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Get(key, defaultValue, sharedName));

        public Task<float> GetAsync(string key, float defaultValue = 0f, string? sharedName = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Get(key, defaultValue, sharedName));

        public Task<long> GetAsync(string key, long defaultValue = 0L, string? sharedName = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Get(key, defaultValue, sharedName));

        public Task<DateTime> GetAsync(string key, DateTime defaultValue, string? sharedName = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Get(key, defaultValue, sharedName));

        public Task SetAsync(string key, string value, string? sharedName = null,
            CancellationToken cancellationToken = default)
        {
            Set(key, value, sharedName);
            return Task.CompletedTask;
        }

        public Task SetAsync(string key, int value, string? sharedName = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetAsync(string key, bool value, string? sharedName = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetAsync(string key, double value, string? sharedName = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetAsync(string key, float value, string? sharedName = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetAsync(string key, long value, string? sharedName = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetAsync(string key, DateTime value, string? sharedName = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, string? sharedName = null,
            CancellationToken cancellationToken = default)
        {
            Remove(key, sharedName);
            return Task.CompletedTask;
        }
    }

    private static string VersionLegacyPath(IStorageService storage) =>
        Path.Combine(storage.StorageRoot, AppConstants.FilePaths.MediaIndexVersionLegacyFileName);

    [Fact]
    public async Task VersionFileExists_preferences_key_implies_true_without_legacy_file_check()
    {
        var prefs = new FakePrefs();
        prefs.Set(AppConstants.GeneralSettingsKeys.MediaIndexVersion, "stored");
        var storage = new RecordingStorage(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        Assert.False(await storage.FileExists(VersionLegacyPath(storage)));

        var sut = new MediaIndexVersionService(TestLogging.CreateLogger(), storage, new StubVersionFinder("any"), prefs);

        Assert.True(await sut.VersionFileExistsAsync());
    }

    [Fact]
    public async Task ReadVersion_migrates_from_legacy_dat_to_preferences_when_prefs_missing()
    {
        var legacyPath =
            Path.Combine(@"Z:\media-root", AppConstants.FilePaths.MediaIndexVersionLegacyFileName);
        var storage = new RecordingStorage(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [legacyPath] = "legacy-99",
        });
        var prefs = new FakePrefs();

        var sut = new MediaIndexVersionService(TestLogging.CreateLogger(), storage, new StubVersionFinder("x"), prefs);

        Assert.Equal("legacy-99", await sut.ReadVersionAsync());
        Assert.True(prefs.ContainsKey(AppConstants.GeneralSettingsKeys.MediaIndexVersion));
        Assert.Equal("legacy-99",
            prefs.Get(AppConstants.GeneralSettingsKeys.MediaIndexVersion, defaultValue: ""));
    }

    [Fact]
    public async Task IsVersionCurrent_compares_finder_name_to_ReadVersion_snapshot()
    {
        var prefs = new FakePrefs();
        prefs.Set(AppConstants.GeneralSettingsKeys.MediaIndexVersion, "build-a");

        var storage = new RecordingStorage(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        var sutSame = new MediaIndexVersionService(TestLogging.CreateLogger(), storage, new StubVersionFinder("build-a"),
            prefs);
        Assert.True(await sutSame.IsVersionCurrentAsync());

        var sutMismatch =
            new MediaIndexVersionService(TestLogging.CreateLogger(), storage, new StubVersionFinder("build-b"),
                prefs);
        Assert.False(await sutMismatch.IsVersionCurrentAsync());
    }

    [Fact]
    public async Task SaveCurrentVersion_writes_preferences_and_legacy_compat_file()
    {
        var storage = new RecordingStorage(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        var prefs = new FakePrefs();
        var sut = new MediaIndexVersionService(TestLogging.CreateLogger(), storage,
            new StubVersionFinder("app-3.14"), prefs);

        await sut.SaveCurrentVersionAsync();

        Assert.Equal("app-3.14",
            prefs.Get(AppConstants.GeneralSettingsKeys.MediaIndexVersion, defaultValue: ""));
        Assert.Contains(storage.Saves, s =>
            s.Dir == storage.StorageRoot
            && s.FileName == AppConstants.FilePaths.MediaIndexVersionLegacyFileName
            && s.Contents == "app-3.14");
    }
}
