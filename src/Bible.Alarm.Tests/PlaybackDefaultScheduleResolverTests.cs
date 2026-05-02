#nullable enable

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
}
