#nullable enable

using Bible.Alarm.Common.Services.Storage;
using Microsoft.Maui.Storage;

namespace Bible.Alarm.Tests;

public sealed class ThreadSafePreferencesServiceTests
{
    private sealed class InMemoryPreferences : IPreferences
    {
        private readonly Dictionary<(string? Shared, string Key), object?> store = new();

        public int SetCallCount { get; private set; }

        public bool ContainsKey(string key, string? sharedName = null) =>
            store.ContainsKey((sharedName, key));

        public void Remove(string key, string? sharedName = null) =>
            store.Remove((sharedName, key));

        public void Clear(string? sharedName = null)
        {
            foreach (var entry in store.Keys.Where(k => k.Shared == sharedName).ToList())
            {
                store.Remove(entry);
            }
        }

        public void Set<T>(string key, T value, string? sharedName = null)
        {
            SetCallCount++;
            store[(sharedName, key)] = value;
        }

        public T Get<T>(string key, T defaultValue, string? sharedName = null) =>
            store.TryGetValue((sharedName, key), out var value) && value is T typed
                ? typed
                : defaultValue;
    }

    private sealed class ThrowingPreferences : IPreferences
    {
        public Exception GetException { get; set; } = new InvalidOperationException("get failed");
        public Exception SetException { get; set; } = new InvalidOperationException("set failed");
        public Exception RemoveException { get; set; } = new InvalidOperationException("remove failed");
        public Exception ClearException { get; set; } = new InvalidOperationException("clear failed");
        public Exception ContainsException { get; set; } = new InvalidOperationException("contains failed");

        public bool ThrowOnGet { get; set; }
        public bool ThrowOnSet { get; set; }
        public bool ThrowOnRemove { get; set; }
        public bool ThrowOnClear { get; set; }
        public bool ThrowOnContains { get; set; }

        public bool ContainsKey(string key, string? sharedName = null)
        {
            if (ThrowOnContains)
            {
                throw ContainsException;
            }

            return false;
        }

        public void Remove(string key, string? sharedName = null)
        {
            if (ThrowOnRemove)
            {
                throw RemoveException;
            }
        }

        public void Clear(string? sharedName = null)
        {
            if (ThrowOnClear)
            {
                throw ClearException;
            }
        }

        public void Set<T>(string key, T value, string? sharedName = null)
        {
            if (ThrowOnSet)
            {
                throw SetException;
            }
        }

        public T Get<T>(string key, T defaultValue, string? sharedName = null)
        {
            if (ThrowOnGet)
            {
                throw GetException;
            }

            return defaultValue;
        }
    }

    private sealed class FlakyIoPreferences : IPreferences
    {
        private readonly Dictionary<string, object?> store = new();
        private int remainingFailures;

        public FlakyIoPreferences(int failuresBeforeSuccess) =>
            remainingFailures = failuresBeforeSuccess;

        public int AttemptCount { get; private set; }

        public bool ContainsKey(string key, string? sharedName = null) => store.ContainsKey(key);

        public void Remove(string key, string? sharedName = null) => store.Remove(key);

        public void Clear(string? sharedName = null) => store.Clear();

        public void Set<T>(string key, T value, string? sharedName = null)
        {
            AttemptCount++;
            if (remainingFailures > 0)
            {
                remainingFailures--;
                throw new IOException("locked");
            }

            store[key] = value;
        }

        public T Get<T>(string key, T defaultValue, string? sharedName = null) =>
            store.TryGetValue(key, out var value) && value is T typed ? typed : defaultValue;
    }

    private static ThreadSafePreferencesService CreateSut(IPreferences preferences) =>
        new(preferences);

    [Fact]
    public void Get_missing_keys_return_typed_defaults()
    {
        var sut = CreateSut(new InMemoryPreferences());
        var fallbackDate = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        Assert.Equal("fallback", sut.Get("s", "fallback"));
        Assert.Equal(42, sut.Get("i", 42));
        Assert.True(sut.Get("b", true));
        Assert.Equal(1.5, sut.Get("d", 1.5));
        Assert.Equal(2.5f, sut.Get("f", 2.5f));
        Assert.Equal(99L, sut.Get("l", 99L));
        Assert.Equal(fallbackDate, sut.Get("dt", fallbackDate));
    }

    [Fact]
    public void Set_and_Get_round_trip_all_supported_types()
    {
        var prefs = new InMemoryPreferences();
        var sut = CreateSut(prefs);
        var when = new DateTime(2024, 6, 15, 12, 30, 0, DateTimeKind.Utc);

        sut.Set("s", "hello");
        sut.Set("i", 7);
        sut.Set("b", true);
        sut.Set("d", 3.14);
        sut.Set("f", 1.25f);
        sut.Set("l", 1234567890123L);
        sut.Set("dt", when);

        Assert.Equal("hello", sut.Get("s", string.Empty));
        Assert.Equal(7, sut.Get("i", 0));
        Assert.True(sut.Get("b", false));
        Assert.Equal(3.14, sut.Get("d", 0.0));
        Assert.Equal(1.25f, sut.Get("f", 0f));
        Assert.Equal(1234567890123L, sut.Get("l", 0L));
        Assert.Equal(when, sut.Get("dt", DateTime.MinValue));
        Assert.Equal(7, prefs.SetCallCount);
    }

    [Fact]
    public void ContainsKey_Remove_and_Clear_work()
    {
        var sut = CreateSut(new InMemoryPreferences());

        Assert.False(sut.ContainsKey("a"));
        sut.Set("a", "1");
        sut.Set("b", "2");
        Assert.True(sut.ContainsKey("a"));
        Assert.True(sut.ContainsKey("b"));

        sut.Remove("a");
        Assert.False(sut.ContainsKey("a"));
        Assert.True(sut.ContainsKey("b"));

        sut.Clear();
        Assert.False(sut.ContainsKey("b"));
    }

    [Fact]
    public void SharedName_isolates_keys()
    {
        var sut = CreateSut(new InMemoryPreferences());

        sut.Set("k", "default");
        sut.Set("k", "shared", "group");

        Assert.Equal("default", sut.Get("k", string.Empty));
        Assert.Equal("shared", sut.Get("k", string.Empty, "group"));
        Assert.True(sut.ContainsKey("k"));
        Assert.True(sut.ContainsKey("k", "group"));

        sut.Remove("k", "group");
        Assert.True(sut.ContainsKey("k"));
        Assert.False(sut.ContainsKey("k", "group"));

        sut.Clear("group");
        sut.Set("k", "again", "group");
        sut.Clear();
        Assert.False(sut.ContainsKey("k"));
        Assert.True(sut.ContainsKey("k", "group"));
    }

    [Fact]
    public void Get_returns_default_when_preferences_throw()
    {
        var prefs = new ThrowingPreferences { ThrowOnGet = true };
        var sut = CreateSut(prefs);
        var fallbackDate = new DateTime(2019, 5, 1);

        Assert.Equal("d", sut.Get("s", "d"));
        Assert.Equal(5, sut.Get("i", 5));
        Assert.True(sut.Get("b", true));
        Assert.Equal(9.9, sut.Get("d", 9.9));
        Assert.Equal(8.8f, sut.Get("f", 8.8f));
        Assert.Equal(4L, sut.Get("l", 4L));
        Assert.Equal(fallbackDate, sut.Get("dt", fallbackDate));
    }

    [Fact]
    public void ContainsKey_returns_false_when_preferences_throw()
    {
        var prefs = new ThrowingPreferences { ThrowOnContains = true };
        var sut = CreateSut(prefs);

        Assert.False(sut.ContainsKey("any"));
    }

    [Fact]
    public void Remove_wraps_failures_in_InvalidOperationException()
    {
        var prefs = new ThrowingPreferences { ThrowOnRemove = true };
        var sut = CreateSut(prefs);

        var ex = Assert.Throws<InvalidOperationException>(() => sut.Remove("k"));
        Assert.Contains("k", ex.Message, StringComparison.Ordinal);
        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public void Clear_wraps_failures_in_InvalidOperationException()
    {
        var prefs = new ThrowingPreferences { ThrowOnClear = true };
        var sut = CreateSut(prefs);

        var ex = Assert.Throws<InvalidOperationException>(() => sut.Clear());
        Assert.Contains("clearing", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public void Set_wraps_non_io_failures_in_InvalidOperationException()
    {
        var prefs = new ThrowingPreferences { ThrowOnSet = true };
        var sut = CreateSut(prefs);

        var ex = Assert.Throws<InvalidOperationException>(() => sut.Set("k", "v"));
        Assert.Contains("k", ex.Message, StringComparison.Ordinal);
        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public void Set_retries_transient_IOException_then_succeeds()
    {
        var prefs = new FlakyIoPreferences(failuresBeforeSuccess: 2);
        var sut = CreateSut(prefs);

        sut.Set("k", "v");

        Assert.Equal("v", sut.Get("k", string.Empty));
        Assert.Equal(3, prefs.AttemptCount);
    }

    [Fact]
    public async Task GetAsync_and_SetAsync_round_trip_all_supported_types()
    {
        var sut = CreateSut(new InMemoryPreferences());
        var when = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        await sut.SetAsync("s", "async");
        await sut.SetAsync("i", 11);
        await sut.SetAsync("b", false);
        await sut.SetAsync("d", 2.71);
        await sut.SetAsync("f", 0.5f);
        await sut.SetAsync("l", 42L);
        await sut.SetAsync("dt", when);

        Assert.Equal("async", await sut.GetAsync("s", string.Empty));
        Assert.Equal(11, await sut.GetAsync("i", 0));
        Assert.False(await sut.GetAsync("b", true));
        Assert.Equal(2.71, await sut.GetAsync("d", 0.0));
        Assert.Equal(0.5f, await sut.GetAsync("f", 0f));
        Assert.Equal(42L, await sut.GetAsync("l", 0L));
        Assert.Equal(when, await sut.GetAsync("dt", DateTime.MinValue));
    }

    [Fact]
    public async Task GetAsync_returns_defaults_when_preferences_throw()
    {
        var prefs = new ThrowingPreferences { ThrowOnGet = true };
        var sut = CreateSut(prefs);
        var fallbackDate = new DateTime(2018, 3, 4);

        Assert.Equal("d", await sut.GetAsync("s", "d"));
        Assert.Equal(5, await sut.GetAsync("i", 5));
        Assert.True(await sut.GetAsync("b", true));
        Assert.Equal(9.9, await sut.GetAsync("d", 9.9));
        Assert.Equal(8.8f, await sut.GetAsync("f", 8.8f));
        Assert.Equal(4L, await sut.GetAsync("l", 4L));
        Assert.Equal(fallbackDate, await sut.GetAsync("dt", fallbackDate));
    }

    [Fact]
    public async Task SetAsync_retries_transient_IOException_then_succeeds()
    {
        var prefs = new FlakyIoPreferences(failuresBeforeSuccess: 1);
        var sut = CreateSut(prefs);

        await sut.SetAsync("k", 99);

        Assert.Equal(99, await sut.GetAsync("k", 0));
        Assert.Equal(2, prefs.AttemptCount);
    }

    [Fact]
    public async Task RemoveAsync_removes_key()
    {
        var sut = CreateSut(new InMemoryPreferences());
        sut.Set("k", "v");

        await sut.RemoveAsync("k");

        Assert.False(sut.ContainsKey("k"));
    }

    [Fact]
    public async Task RemoveAsync_wraps_failures_in_InvalidOperationException()
    {
        var prefs = new ThrowingPreferences { ThrowOnRemove = true };
        var sut = CreateSut(prefs);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RemoveAsync("k"));
        Assert.Contains("k", ex.Message, StringComparison.Ordinal);
        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public async Task GetAsync_missing_keys_return_typed_defaults()
    {
        var sut = CreateSut(new InMemoryPreferences());
        var fallbackDate = new DateTime(2021, 8, 9);

        Assert.Equal("fallback", await sut.GetAsync("s", "fallback"));
        Assert.Equal(42, await sut.GetAsync("i", 42));
        Assert.True(await sut.GetAsync("b", true));
        Assert.Equal(1.5, await sut.GetAsync("d", 1.5));
        Assert.Equal(2.5f, await sut.GetAsync("f", 2.5f));
        Assert.Equal(99L, await sut.GetAsync("l", 99L));
        Assert.Equal(fallbackDate, await sut.GetAsync("dt", fallbackDate));
    }
}
