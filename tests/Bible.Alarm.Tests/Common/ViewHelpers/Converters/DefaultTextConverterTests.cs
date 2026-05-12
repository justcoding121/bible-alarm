#nullable enable

using System.Globalization;
using Bible.Alarm.Common.ViewHelpers.Converters;

namespace Bible.Alarm.Tests;

public sealed class DefaultTextConverterTests
{
    private static readonly CultureInfo Cul = CultureInfo.InvariantCulture;

    [Fact]
    public void Convert_non_whitespace_string_returns_input()
    {
        var sut = new DefaultTextConverter();

        Assert.Equal("Hello", sut.Convert("Hello", typeof(string), null, Cul));
    }

    [Fact]
    public void Convert_null_uses_non_breaking_space_when_no_parameter()
    {
        var sut = new DefaultTextConverter();

        Assert.Equal("\u00A0", sut.Convert(null, typeof(string), null, Cul));
    }

    [Fact]
    public void Convert_whitespace_uses_string_parameter_default()
    {
        var sut = new DefaultTextConverter();

        Assert.Equal("fallback", sut.Convert("  ", typeof(string), "fallback", Cul));
    }

    [Fact]
    public void ConvertBack_returns_value_unchanged()
    {
        var sut = new DefaultTextConverter();

        Assert.Same("x", sut.ConvertBack("x", typeof(string), null, Cul));
    }
}
