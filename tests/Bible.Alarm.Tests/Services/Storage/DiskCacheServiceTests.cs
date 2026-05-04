#nullable enable

using Bible.Alarm.Common.Interfaces.Storage;
using Bible.Alarm.Services.Storage;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class DiskCacheServiceTests
{
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
    public async Task GetCacheKey_rejects_whitespace_via_GetOrSetAsync()
    {
        var sut = new DiskCacheService(TestLogging.CreateLogger(), new FakePrefs());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.GetOrSetAsync("  ", () => Task.FromResult(1)));
    }

    [Fact]
    public async Task GetOrSetAsync_second_call_uses_cache_without_second_factory_run()
    {
        var prefs = new FakePrefs();
        var sut = new DiskCacheService(TestLogging.CreateLogger(), prefs);
        var factoryCalls = 0;

        Task<string> Factory()
        {
            factoryCalls++;
            return Task.FromResult("alpha");
        }

        var first = await sut.GetOrSetAsync("k1", Factory);
        var second = await sut.GetOrSetAsync("k1", Factory);

        Assert.Equal("alpha", first);
        Assert.Equal("alpha", second);
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public async Task GetAsync_returns_deserialized_value()
    {
        var prefs = new FakePrefs();
        var sut = new DiskCacheService(TestLogging.CreateLogger(), prefs);

        await sut.SetAsync("nums", new[] { 3, 4 });

        var got = await sut.GetAsync<int[]>("nums");

        Assert.NotNull(got);
        Assert.Equal(new[] { 3, 4 }, got);
    }

    [Fact]
    public async Task Remove_and_ContainsKey_roundtrip()
    {
        var prefs = new FakePrefs();
        var sut = new DiskCacheService(TestLogging.CreateLogger(), prefs);

        Assert.False(sut.ContainsKey("gone"));

        await sut.GetOrSetAsync("gone", () => Task.FromResult(7));

        Assert.True(sut.ContainsKey("gone"));
        sut.Remove("gone");
        Assert.False(sut.ContainsKey("gone"));
    }
}
