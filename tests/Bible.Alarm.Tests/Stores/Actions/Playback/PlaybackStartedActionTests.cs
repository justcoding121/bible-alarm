#nullable enable

using Bible.Alarm.Stores.Actions.Playback;

namespace Bible.Alarm.Tests;

public sealed class PlaybackStartedActionTests
{
    [Fact]
    public void Constructor_sets_ScheduleId()
    {
        var sut = new PlaybackStartedAction(77);

        Assert.Equal(77, sut.ScheduleId);
    }
}
