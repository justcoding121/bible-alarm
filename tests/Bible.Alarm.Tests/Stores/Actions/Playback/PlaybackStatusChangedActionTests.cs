#nullable enable

using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores.Actions.Playback;

namespace Bible.Alarm.Tests;

public sealed class PlaybackStatusChangedActionTests
{
    [Fact]
    public void Constructor_sets_Status()
    {
        var sut = new PlaybackStatusChangedAction(PlayStatus.Paused);

        Assert.Equal(PlayStatus.Paused, sut.Status);
    }
}
