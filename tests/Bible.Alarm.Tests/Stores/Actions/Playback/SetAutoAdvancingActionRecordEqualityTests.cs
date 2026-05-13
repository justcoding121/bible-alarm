#nullable enable

using Bible.Alarm.Stores.Actions.Playback;

namespace Bible.Alarm.Tests;

public sealed class SetAutoAdvancingActionRecordEqualityTests
{
    [Fact]
    public void Instances_with_same_flag_are_equal()
    {
        var a = new SetAutoAdvancingAction(true);
        var b = new SetAutoAdvancingAction(a.IsAutoAdvancing);
        Assert.Equal(a, b);
    }
}
