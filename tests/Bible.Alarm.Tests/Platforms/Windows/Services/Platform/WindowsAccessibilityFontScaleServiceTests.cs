#nullable enable

using Bible.Alarm.Platforms.Windows.Services.Platform;

namespace Bible.Alarm.Tests;

[Trait("Platform", "Windows")]
public sealed class WindowsAccessibilityFontScaleServiceTests
{
    [Fact]
    public void FontScale_reports_positive_finite_value_after_construction()
    {
        using var sut = new WindowsAccessibilityFontScaleService();

        Assert.True(double.IsFinite(sut.FontScale) && sut.FontScale > 0);
    }

    [Fact]
    public void Dispose_can_be_called_twice_without_throw()
    {
        var sut = new WindowsAccessibilityFontScaleService();

        sut.Dispose();
        sut.Dispose();
    }
}
