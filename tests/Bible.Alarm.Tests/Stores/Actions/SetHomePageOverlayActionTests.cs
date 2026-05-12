#nullable enable

using Bible.Alarm.Stores.Actions;

namespace Bible.Alarm.Tests;

public sealed class SetHomePageOverlayActionTests
{
    [Fact]
    public void Init_sets_IsVisible()
    {
        var sut = new SetHomePageOverlayAction { IsVisible = true };

        Assert.True(sut.IsVisible);
    }
}
