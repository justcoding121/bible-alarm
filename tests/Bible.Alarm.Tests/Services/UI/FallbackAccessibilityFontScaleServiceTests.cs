#nullable enable

using Bible.Alarm.Services.UI.FontCompat;

namespace Bible.Alarm.Tests;

public sealed class FallbackAccessibilityFontScaleServiceTests
{
    [Fact]
    public void FontScale_returns_one()
    {
        var sut = new FallbackAccessibilityFontScaleService();
        Assert.Equal(1.0, sut.FontScale);
    }

    [Fact]
    public void FontScaleChanged_can_subscribe()
    {
        var sut = new FallbackAccessibilityFontScaleService();
        sut.FontScaleChanged += (_, _) => { };
    }
}
