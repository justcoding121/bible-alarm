#nullable enable

using System.Globalization;
using Bible.Alarm.Common.ViewHelpers.Converters;

namespace Bible.Alarm.Tests;

public sealed class IsStringNotEmptyConverterTests
{
    private static readonly CultureInfo Cul = CultureInfo.InvariantCulture;

    [Fact]
    public void Convert_non_empty_string_returns_true()
    {
        var sut = new IsStringNotEmptyConverter();

        Assert.True((bool)sut.Convert("a", typeof(bool), null, Cul)!);
    }

    [Fact]
    public void Convert_whitespace_string_returns_false()
    {
        var sut = new IsStringNotEmptyConverter();

        Assert.False((bool)sut.Convert(" \n", typeof(bool), null, Cul)!);
    }

    [Fact]
    public void Convert_non_string_returns_false()
    {
        var sut = new IsStringNotEmptyConverter();

        Assert.False((bool)sut.Convert(42, typeof(bool), null, Cul)!);
    }

    [Fact]
    public void ConvertBack_throws()
    {
        var sut = new IsStringNotEmptyConverter();

        Assert.Throws<NotImplementedException>(() =>
            sut.ConvertBack(true, typeof(string), null, Cul));
    }
}
