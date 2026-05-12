#nullable enable

using Bible.Alarm.Stores.Actions.Playback;

namespace Bible.Alarm.Tests;

public sealed class PlaybackTrackTransitionEndedActionTests
{
    [Fact]
    public void FluxorPayloadVersion_is_expected()
    {
        Assert.Equal((byte)1, PlaybackTrackTransitionEndedAction.FluxorPayloadVersion);
    }
}
