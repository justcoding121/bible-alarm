#nullable enable

using Bible.Alarm.Stores.Actions.Playback;

namespace Bible.Alarm.Tests;

public sealed class PlaybackTrackTransitionStartedActionRecordEqualityTests
{
    [Fact]
    public void Parameterless_instances_are_equal()
    {
        var a = new PlaybackTrackTransitionStartedAction();
        var b = new PlaybackTrackTransitionStartedAction();
        Assert.Equal(a, b);
    }
}
