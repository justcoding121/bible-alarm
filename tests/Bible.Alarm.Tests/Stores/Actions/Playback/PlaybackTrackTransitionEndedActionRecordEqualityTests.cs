#nullable enable

using Bible.Alarm.Stores.Actions.Playback;

namespace Bible.Alarm.Tests;

public sealed class PlaybackTrackTransitionEndedActionRecordEqualityTests
{
    [Fact]
    public void Parameterless_instances_are_equal()
    {
        var a = new PlaybackTrackTransitionEndedAction();
        var b = new PlaybackTrackTransitionEndedAction();
        Assert.Equal(a, b);
    }
}
