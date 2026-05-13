#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class ToastPopupOffsetsTests
{
    [Fact]
    public void HorizontalCenterOffset_subtracts_inner_from_outer_then_halves()
    {
        Assert.Equal(150, ToastPopupOffsets.HorizontalCenterOffset(800, 500));
    }

    [Fact]
    public void VerticalOffsetAboveBottom_subtracts_content_and_margin_from_height()
    {
        Assert.Equal(520, ToastPopupOffsets.VerticalOffsetAboveBottom(900, 300, 80));
    }
}
