using Bible.Alarm.Shared.Models.Enums;

namespace Bible.Alarm.Shared.Tests;

public sealed class PlayTypeTests
{
    [Fact]
    public void Values_match_schedule_and_media_defaults()
    {
        Assert.Equal(0, (int)PlayType.Music);
        Assert.Equal(1, (int)PlayType.Bible);
    }
}
