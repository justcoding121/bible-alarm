#nullable enable

using Bible.Alarm.Stores.Actions.Playback;

namespace Bible.Alarm.Tests;

public sealed class PlaybackDurationChangedActionTests
{
    [Fact]
    public void Init_sets_Duration()
    {
        var sut = new PlaybackDurationChangedAction { Duration = TimeSpan.FromSeconds(90) };

        Assert.Equal(TimeSpan.FromSeconds(90), sut.Duration);
    }
}
