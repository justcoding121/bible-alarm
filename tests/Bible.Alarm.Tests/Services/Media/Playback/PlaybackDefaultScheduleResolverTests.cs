#nullable enable

using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Stores;
using Bible.Alarm.Tests.Support;
using Fluxor;

namespace Bible.Alarm.Tests;

public sealed class PlaybackDefaultScheduleResolverTests
{
    private sealed class FakePlaybackState(PlaybackState value) : IState<PlaybackState>
    {
        public PlaybackState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    [Fact]
    public void Resolve_returns_positive_DefaultScheduleId_from_fluxor_state()
    {
        var logger = TestLogging.CreateLogger();
        var state = new FakePlaybackState(new PlaybackState { DefaultScheduleId = 901 });

        var resolved = PlaybackDefaultScheduleResolver.Resolve(state, logger);

        Assert.Equal(901, resolved);
    }

    [Fact]
    public void Resolve_returns_null_when_fluxor_default_is_missing()
    {
        var logger = TestLogging.CreateLogger();
        var state = new FakePlaybackState(new PlaybackState { DefaultScheduleId = null });

        var resolved = PlaybackDefaultScheduleResolver.Resolve(state, logger);

        Assert.Null(resolved);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Resolve_returns_null_when_fluxor_default_is_not_positive(int invalidId)
    {
        var logger = TestLogging.CreateLogger();
        var state = new FakePlaybackState(new PlaybackState { DefaultScheduleId = invalidId });

        var resolved = PlaybackDefaultScheduleResolver.Resolve(state, logger);

        Assert.Null(resolved);
    }

    [Fact]
    public void Resolve_uses_last_played_schedule_from_preferences_when_fluxor_default_missing()
    {
        if (!TryBootstrapMauiAppForPreferences())
        {
            return;
        }

        try
        {
            LastPlayedMetadataHelper.ClearLastPlayedMetadata();
            LastPlayedMetadataHelper.SaveLastPlayedMetadata("Genesis 1", "NWT", scheduleId: 77);

            var logger = TestLogging.CreateLogger();
            var state = new FakePlaybackState(new PlaybackState { DefaultScheduleId = null });

            var resolved = PlaybackDefaultScheduleResolver.Resolve(state, logger);

            Assert.Equal(77, resolved);
        }
        finally
        {
            TryClearLastPlayedMetadata();
        }
    }

    [Fact]
    public void Resolve_prefers_fluxor_default_over_preferences()
    {
        if (!TryBootstrapMauiAppForPreferences())
        {
            return;
        }

        try
        {
            LastPlayedMetadataHelper.ClearLastPlayedMetadata();
            LastPlayedMetadataHelper.SaveLastPlayedMetadata("Genesis 1", "NWT", scheduleId: 77);

            var logger = TestLogging.CreateLogger();
            var state = new FakePlaybackState(new PlaybackState { DefaultScheduleId = 12 });

            var resolved = PlaybackDefaultScheduleResolver.Resolve(state, logger);

            Assert.Equal(12, resolved);
        }
        finally
        {
            TryClearLastPlayedMetadata();
        }
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
            // Headless xUnit on Windows cannot initialize WinUI Preferences without a real app host.
            return false;
        }
    }

    private static void TryClearLastPlayedMetadata()
    {
        try
        {
            LastPlayedMetadataHelper.ClearLastPlayedMetadata();
        }
        catch
        {
        }
    }
}
