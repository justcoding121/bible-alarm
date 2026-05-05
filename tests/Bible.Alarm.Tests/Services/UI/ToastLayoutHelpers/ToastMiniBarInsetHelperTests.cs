#nullable enable

using Bible.Alarm.Services.UI.ToastLayoutHelpers;

namespace Bible.Alarm.Tests;

public sealed class ToastMiniBarInsetHelperTests
{
    [Fact]
    public void GetBottomInsetDip_returns_zero_when_mini_bar_view_model_uninitialized()
    {
        Assert.Equal(0, ToastMiniBarInsetHelper.GetBottomInsetDip(null, lastMeasuredHeightDip: 0));
    }
}
