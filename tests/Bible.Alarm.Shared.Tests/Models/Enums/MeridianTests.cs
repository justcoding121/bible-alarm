using Bible.Alarm.Shared.Models.Enums;

namespace Bible.Alarm.Shared.Tests;

public sealed class MeridianTests
{
    [Fact]
    public void Values_are_sequential_for_UI_persistence()
    {
        Assert.Equal(0, (int)Meridian.Am);
        Assert.Equal(1, (int)Meridian.Pm);
    }
}
