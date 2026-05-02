#nullable enable

using Bible.Alarm.Common.Interfaces.Platform;
using Bible.Alarm.Common.Interfaces.Storage;
using Bible.Alarm.Services.Database;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class ScheduleDatabaseVersionServiceTests
{
    private const string VersionKey = "ScheduleDatabaseVersion";

    private sealed class FakeVersionFinder(string versionName) : IVersionFinder
    {
        public string GetVersionName() => versionName;
    }

    private sealed class ThrowingVersionFinder : IVersionFinder
    {
        public string GetVersionName() =>
            throw new InvalidOperationException("no version");
    }

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

    [Fact]
    public async Task IsVersionCurrentAsync_false_when_key_missing_or_empty()
    {
        var prefs = new FakePrefs();
        var sut = new ScheduleDatabaseVersionService(TestLogging.CreateLogger(), new FakeVersionFinder("9"), prefs);

        Assert.False(await sut.IsVersionCurrentAsync());

        prefs.Set(VersionKey, "");
        Assert.False(await sut.IsVersionCurrentAsync());
    }

    [Fact]
    public async Task IsVersionCurrentAsync_matches_stored_and_current_names()
    {
        var prefs = new FakePrefs();
        prefs.Set(VersionKey, "build-a");
        var sut = new ScheduleDatabaseVersionService(TestLogging.CreateLogger(), new FakeVersionFinder("build-a"), prefs);

        Assert.True(await sut.IsVersionCurrentAsync());

        var sutOther = new ScheduleDatabaseVersionService(TestLogging.CreateLogger(), new FakeVersionFinder("build-b"),
            prefs);

        Assert.False(await sutOther.IsVersionCurrentAsync());
    }

    [Fact]
    public async Task IsVersionCurrentAsync_false_when_version_lookup_fails()
    {
        var prefs = new FakePrefs();
        prefs.Set(VersionKey, "x");
        var sut = new ScheduleDatabaseVersionService(TestLogging.CreateLogger(), new ThrowingVersionFinder(), prefs);

        Assert.False(await sut.IsVersionCurrentAsync());
    }

    [Fact]
    public async Task SaveCurrentVersionAsync_writes_finder_output()
    {
        var prefs = new FakePrefs();
        var sut = new ScheduleDatabaseVersionService(TestLogging.CreateLogger(), new FakeVersionFinder("rel-2"), prefs);

        await sut.SaveCurrentVersionAsync();

        Assert.Equal("rel-2", prefs.Get(VersionKey, string.Empty));
    }

    [Fact]
    public async Task SaveCurrentVersionAsync_wraps_finder_failure()
    {
        var prefs = new FakePrefs();
        var sut = new ScheduleDatabaseVersionService(TestLogging.CreateLogger(), new ThrowingVersionFinder(), prefs);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SaveCurrentVersionAsync());
    }
}
