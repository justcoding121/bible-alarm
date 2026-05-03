#nullable enable

using System.Globalization;
using Bible.Alarm.Common.ViewHelpers.Converters;

namespace Bible.Alarm.Tests;

public sealed class IsStringNotEmptyConverterTests
{
    private static readonly CultureInfo Cul = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData("a", true)]
    [InlineData("  x ", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void Convert_string_expected(string? input, bool expected)
    {
        var sut = new IsStringNotEmptyConverter();

        Assert.Equal(expected, sut.Convert(input, typeof(bool), null, Cul));
    }

    [Fact]
    public void Convert_non_string_is_false()
    {
        var sut = new IsStringNotEmptyConverter();

        Assert.False((bool)sut.Convert(7, typeof(bool), null, Cul)!);
    }

    [Fact]
    public void ConvertBack_throws()
    {
        var sut = new IsStringNotEmptyConverter();

        Assert.Throws<NotImplementedException>(() => sut.ConvertBack(true, typeof(string), null, Cul));
    }
}
