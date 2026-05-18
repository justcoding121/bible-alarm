#nullable enable

using Bible.Alarm.Common;
using Bible.Alarm.Common.Services.Storage;
using Bible.Alarm.Tests.Support;

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

    [Fact]
    public void Get_int_and_bool_missing_keys_return_defaults()
    {
        var sut = new ThreadSafePreferencesService();
        var key = $"unit_test_prefs_{Guid.NewGuid():N}";

        Assert.Equal(42, sut.Get($"{key}_int", 42));
        Assert.True(sut.Get($"{key}_bool", true));
    }

    [Fact]
    public void Set_and_get_round_trip_when_preferences_available()
    {
        if (!TryBootstrapMauiAppForPreferences())
        {
            return;
        }

        var sut = new ThreadSafePreferencesService();
        var key = $"unit_test_prefs_{Guid.NewGuid():N}";

        sut.Set(key, "stored");
        sut.Set($"{key}_int", 7);
        sut.Set($"{key}_bool", false);

        Assert.Equal("stored", sut.Get(key, string.Empty));
        Assert.Equal(7, sut.Get($"{key}_int", 0));
        Assert.False(sut.Get($"{key}_bool", true));

        sut.Remove(key);
        sut.Remove($"{key}_int");
        sut.Remove($"{key}_bool");
    }

    private static bool TryBootstrapMauiAppForPreferences()
    {
        if (MauiAppHolder.IsInitialized)
        {
            return true;
        }

        try
        {
            MauiAppHolder.CreateAndStore();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
