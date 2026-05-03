#nullable enable

using System.Globalization;
using Bible.Alarm.Common.ViewHelpers.Converters;
using Microsoft.Maui.Controls;

namespace Bible.Alarm.Tests;

public sealed class CommonValueConvertersTests
{
    private static readonly CultureInfo Cul = CultureInfo.InvariantCulture;

    [Fact]
    public void DefaultTextConverter_returns_string_when_non_whitespace()
    {
        var sut = new DefaultTextConverter();

        Assert.Equal("hi", sut.Convert("hi", typeof(string), null, Cul));
    }

    [Fact]
    public void DefaultTextConverter_uses_parameter_or_nbsp_when_empty()
    {
        var sut = new DefaultTextConverter();

        Assert.Equal("fallback", sut.Convert(null, typeof(string), "fallback", Cul));
        Assert.Equal("fallback", sut.Convert("  ", typeof(string), "fallback", Cul));
        Assert.Equal("\u00A0", sut.Convert("", typeof(string), null, Cul));
    }

    [Fact]
    public void DefaultTextConverter_non_string_uses_parameter_or_nbsp()
    {
        var sut = new DefaultTextConverter();

        Assert.Equal("p", sut.Convert(99, typeof(string), "p", Cul));
        Assert.Equal("\u00A0", sut.Convert(99, typeof(string), null, Cul));
    }

    [Fact]
    public void DefaultTextConverter_ConvertBack_returns_value()
    {
        var sut = new DefaultTextConverter();

        Assert.Equal("x", sut.ConvertBack("x", typeof(string), null, Cul));
    }

    [Theory]
    [InlineData(true, 1.0)]
    [InlineData(false, 0.0)]
    public void BoolToOpacityConverter_Convert_maps_bool(bool input, double expected)
    {
        var sut = new BoolToOpacityConverter();

        Assert.Equal(expected, Assert.IsType<double>(sut.Convert(input, typeof(double), null, Cul)));
    }

    [Fact]
    public void BoolToOpacityConverter_Convert_non_bool_is_zero()
    {
        var sut = new BoolToOpacityConverter();

        Assert.Equal(0.0, sut.Convert("yes", typeof(double), null, Cul));
    }

    [Theory]
    [InlineData(0.6, true)]
    [InlineData(0.5, false)]
    [InlineData(0.49, false)]
    public void BoolToOpacityConverter_ConvertBack_maps_double(double opacity, bool expected)
    {
        var sut = new BoolToOpacityConverter();

        Assert.Equal(expected, Assert.IsType<bool>(sut.ConvertBack(opacity, typeof(bool), null, Cul)));
    }

    [Fact]
    public void DoubleToBottomThicknessConverter_Convert_double_sets_bottom()
    {
        var sut = new DoubleToBottomThicknessConverter();

        var t = Assert.IsType<Thickness>(sut.Convert(8.5, typeof(Thickness), null, Cul));

        Assert.Equal(0, t.Left);
        Assert.Equal(0, t.Top);
        Assert.Equal(0, t.Right);
        Assert.Equal(8.5, t.Bottom);
    }

    [Fact]
    public void DoubleToBottomThicknessConverter_Convert_non_double_is_zero_thickness()
    {
        var sut = new DoubleToBottomThicknessConverter();

        var t = Assert.IsType<Thickness>(sut.Convert(8, typeof(Thickness), null, Cul));

        Assert.Equal(Thickness.Zero, t);
    }

    [Fact]
    public void DoubleToBottomThicknessConverter_ConvertBack_reads_bottom()
    {
        var sut = new DoubleToBottomThicknessConverter();

        Assert.Equal(3.0, Assert.IsType<double>(sut.ConvertBack(new Thickness(1, 2, 4, 3), typeof(double), null, Cul)));
    }

    [Fact]
    public void DoubleToBottomThicknessConverter_ConvertBack_non_thickness_is_zero()
    {
        var sut = new DoubleToBottomThicknessConverter();

        Assert.Equal(0.0, sut.ConvertBack("nope", typeof(double), null, Cul));
    }

    [Theory]
    [InlineData("", true)]
    [InlineData(0, true)]
    public void IsNotNullConverter_Convert_truthy_when_value_present(object? value, bool expected)
    {
        var sut = new IsNotNullConverter();

        Assert.Equal(expected, sut.Convert(value, typeof(bool), null, Cul));
    }

    [Fact]
    public void IsNotNullConverter_Convert_false_when_reference_null()
    {
        var sut = new IsNotNullConverter();

        Assert.False((bool)sut.Convert(null, typeof(bool), null, Cul)!);
    }

    [Fact]
    public void IsNotNullConverter_ConvertBack_throws()
    {
        var sut = new IsNotNullConverter();

        Assert.Throws<NotImplementedException>(() => sut.ConvertBack(true, typeof(object), null, Cul));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData(0, false)]
    public void IsNullConverter_Convert_false_when_value_present(object? value, bool expected)
    {
        var sut = new IsNullConverter();

        Assert.Equal(expected, sut.Convert(value, typeof(bool), null, Cul));
    }

    [Fact]
    public void IsNullConverter_Convert_true_when_reference_null()
    {
        var sut = new IsNullConverter();

        Assert.True((bool)sut.Convert(null, typeof(bool), null, Cul)!);
    }

    [Fact]
    public void IsNullConverter_ConvertBack_throws()
    {
        var sut = new IsNullConverter();

        Assert.Throws<NotImplementedException>(() => sut.ConvertBack(false, typeof(object), null, Cul));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void NegateBooleanConverter_Convert_and_ConvertBack_invert(bool input, bool expected)
    {
        var sut = new NegateBooleanConverter();

        Assert.Equal(expected, (bool)sut.Convert(input, typeof(bool), null!, Cul)!);
        Assert.Equal(input, (bool)sut.ConvertBack(expected, typeof(bool), null!, Cul)!);
    }
}
