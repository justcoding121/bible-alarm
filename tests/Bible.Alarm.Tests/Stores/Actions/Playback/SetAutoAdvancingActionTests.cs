#nullable enable

using Bible.Alarm.Stores.Actions.Playback;

namespace Bible.Alarm.Tests;

public sealed class SetAutoAdvancingActionTests
{
    [Fact]
    public void Record_holds_IsAutoAdvancing()
    {
        var sut = new SetAutoAdvancingAction(true);

        Assert.True(sut.IsAutoAdvancing);
    }
}
