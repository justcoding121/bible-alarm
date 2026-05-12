#nullable enable

using System.Globalization;
using Bible.Alarm.Common.ViewHelpers.Converters;

namespace Bible.Alarm.Tests;

public sealed class DoubleToBottomThicknessConverterTests
{
    private static readonly CultureInfo Cul = CultureInfo.InvariantCulture;

    [Fact]
    public void Convert_double_maps_to_bottom_thickness()
    {
        var sut = new DoubleToBottomThicknessConverter();

        var t = Assert.IsType<Thickness>(sut.Convert(12.5, typeof(Thickness), null, Cul));

        Assert.Equal(0, t.Left);
        Assert.Equal(0, t.Top);
        Assert.Equal(0, t.Right);
        Assert.Equal(12.5, t.Bottom);
    }

    [Fact]
    public void Convert_non_double_returns_zero_thickness()
    {
        var sut = new DoubleToBottomThicknessConverter();

        var t = Assert.IsType<Thickness>(sut.Convert("x", typeof(Thickness), null, Cul));

        Assert.Equal(new Thickness(0), t);
    }

    [Fact]
    public void ConvertBack_reads_bottom_component()
    {
        var sut = new DoubleToBottomThicknessConverter();

        Assert.Equal(3.0, sut.ConvertBack(new Thickness(1, 2, 4, 3), typeof(double), null, Cul));
    }

    [Fact]
    public void ConvertBack_non_thickness_returns_zero()
    {
        var sut = new DoubleToBottomThicknessConverter();

        Assert.Equal(0.0, sut.ConvertBack("nope", typeof(double), null, Cul));
    }
}
