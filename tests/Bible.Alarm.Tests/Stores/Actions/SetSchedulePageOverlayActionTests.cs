#nullable enable

using Bible.Alarm.Stores.Actions;

namespace Bible.Alarm.Tests;

public sealed class SetSchedulePageOverlayActionTests
{
    [Fact]
    public void Init_sets_IsVisible()
    {
        var sut = new SetSchedulePageOverlayAction { IsVisible = true };

        Assert.True(sut.IsVisible);
    }
}
