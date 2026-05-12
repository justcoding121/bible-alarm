#nullable enable

using Bible.Alarm.Stores.Actions.Playback;

namespace Bible.Alarm.Tests;

public sealed class PlaybackNavigationChangedActionTests
{
    [Fact]
    public void Constructor_sets_canNavigate_flags()
    {
        var sut = new PlaybackNavigationChangedAction(canPlayNext: true, canPlayPrevious: false);

        Assert.True(sut.CanPlayNext);
        Assert.False(sut.CanPlayPrevious);
    }
}
