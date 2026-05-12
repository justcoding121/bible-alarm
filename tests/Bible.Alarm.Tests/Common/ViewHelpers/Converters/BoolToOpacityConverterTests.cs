#nullable enable

using System.Globalization;
using Bible.Alarm.Common.ViewHelpers.Converters;

namespace Bible.Alarm.Tests;

public sealed class BoolToOpacityConverterTests
{
    private static readonly CultureInfo Cul = CultureInfo.InvariantCulture;

    [Fact]
    public void Convert_true_returns_one()
    {
        var sut = new BoolToOpacityConverter();

        Assert.Equal(1.0, sut.Convert(true, typeof(double), null, Cul));
    }

    [Fact]
    public void Convert_false_returns_zero()
    {
        var sut = new BoolToOpacityConverter();

        Assert.Equal(0.0, sut.Convert(false, typeof(double), null, Cul));
    }

    [Fact]
    public void Convert_non_bool_returns_zero_opacity()
    {
        var sut = new BoolToOpacityConverter();

        Assert.Equal(0.0, sut.Convert("yes", typeof(double), null, Cul));
    }

    [Fact]
    public void ConvertBack_double_above_half_returns_true()
    {
        var sut = new BoolToOpacityConverter();

        Assert.True((bool)sut.ConvertBack(0.6, typeof(bool), null, Cul)!);
    }

    [Fact]
    public void ConvertBack_double_at_half_returns_false()
    {
        var sut = new BoolToOpacityConverter();

        Assert.False((bool)sut.ConvertBack(0.5, typeof(bool), null, Cul)!);
    }

    [Fact]
    public void ConvertBack_non_double_returns_false()
    {
        var sut = new BoolToOpacityConverter();

        Assert.False((bool)sut.ConvertBack("x", typeof(bool), null, Cul)!);
    }
}
