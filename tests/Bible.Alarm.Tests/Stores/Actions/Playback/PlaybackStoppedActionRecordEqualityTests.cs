#nullable enable

using Bible.Alarm.Stores.Actions.Playback;

namespace Bible.Alarm.Tests;

public sealed class PlaybackStoppedActionRecordEqualityTests
{
    [Fact]
    public void Parameterless_instances_are_equal()
    {
        var a = new PlaybackStoppedAction();
        var b = new PlaybackStoppedAction();
        Assert.Equal(a, b);
    }
}
