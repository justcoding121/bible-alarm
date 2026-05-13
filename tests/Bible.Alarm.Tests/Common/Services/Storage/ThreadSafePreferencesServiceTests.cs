#nullable enable

using Bible.Alarm.Common.Services.Storage;

namespace Bible.Alarm.Tests;

public sealed class ThreadSafePreferencesServiceTests
{
    [Fact]
    public void Get_string_missing_key_returns_default()
    {
        var sut = new ThreadSafePreferencesService();
        var key = $"unit_test_prefs_{Guid.NewGuid():N}";

        Assert.Equal("fallback", sut.Get(key, "fallback"));
    }
}
