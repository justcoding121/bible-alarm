#nullable enable

using System.Globalization;
using Bible.Alarm.Common.ViewHelpers.Converters;

namespace Bible.Alarm.Tests;

public sealed class IsNullConverterTests
{
    [Fact]
    public void Convert_returns_true_when_value_is_null()
    {
        var sut = new IsNullConverter();

        Assert.True((bool)sut.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture)!);
    }

    [Fact]
    public void Convert_returns_false_when_value_is_not_null()
    {
        var sut = new IsNullConverter();

        Assert.False((bool)sut.Convert(new object(), typeof(bool), null, CultureInfo.InvariantCulture)!);
    }

    [Fact]
    public void ConvertBack_throws_NotImplementedException()
    {
        var sut = new IsNullConverter();

        Assert.Throws<NotImplementedException>(() =>
            sut.ConvertBack(false, typeof(object), null, CultureInfo.InvariantCulture));
    }
}
