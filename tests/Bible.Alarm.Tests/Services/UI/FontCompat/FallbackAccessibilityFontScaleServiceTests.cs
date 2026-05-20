#nullable enable

using Bible.Alarm.Services.UI.FontCompat;

namespace Bible.Alarm.Tests;

public sealed class FallbackAccessibilityFontScaleServiceTests
{
    [Fact]
    public void FontScale_returns_default_one()
    {
        var sut = new FallbackAccessibilityFontScaleService();

        Assert.Equal(1.0, sut.FontScale);
    }
}
