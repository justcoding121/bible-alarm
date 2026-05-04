#nullable enable

using System.Globalization;
using Bible.Alarm.Common.ViewHelpers.Converters;

namespace Bible.Alarm.Tests;

public sealed class BoolToOpacityConverterTests
{
    private static readonly CultureInfo Cul = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData(true, 1.0)]
    [InlineData(false, 0.0)]
    public void Convert_bool_maps_to_opaque_or_hidden(bool input, double expectedOpacity)
    {
        var sut = new BoolToOpacityConverter();

        var result = sut.Convert(input, typeof(double), null, Cul);

        Assert.Equal(expectedOpacity, Assert.IsType<double>(result));
    }

    [Fact]
    public void Convert_non_bool_returns_hidden()
    {
        var sut = new BoolToOpacityConverter();

        var result = sut.Convert("yes", typeof(double), null, Cul);

        Assert.Equal(0.0, Assert.IsType<double>(result));
    }

    [Theory]
    [InlineData(0.51, true)]
    [InlineData(0.5, false)]
    [InlineData(0.0, false)]
    public void ConvertBack_double_above_half_indicator_is_true_when_threshold(double input, bool expected)
    {
        var sut = new BoolToOpacityConverter();

        Assert.Equal(expected, (bool)sut.ConvertBack(input, typeof(bool), null, Cul)!);
    }

    [Fact]
    public void ConvertBack_non_double_is_false()
    {
        var sut = new BoolToOpacityConverter();

        Assert.False((bool)sut.ConvertBack("0.75", typeof(bool), null, Cul)!);
    }
}
