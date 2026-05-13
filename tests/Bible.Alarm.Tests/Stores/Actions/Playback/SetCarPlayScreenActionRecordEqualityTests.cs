#nullable enable

using Bible.Alarm.Stores.Actions.Playback;

namespace Bible.Alarm.Tests;

public sealed class SetCarPlayScreenActionRecordEqualityTests
{
    [Fact]
    public void Parameterless_instances_are_equal()
    {
        var a = new SetCarPlayScreenAction();
        var b = new SetCarPlayScreenAction();
        Assert.Equal(a, b);
    }
}
