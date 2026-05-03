#nullable enable

using System.Globalization;
using Bible.Alarm.Common.ViewHelpers.Converters;

namespace Bible.Alarm.Tests;

public sealed class RepeatColorConverterTests
{
    private static readonly CultureInfo Cul = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Convert_bool_string_matches_repeat_color(bool repeat)
    {
        var sut = new RepeatColorConverter();
        var expected = repeat ? Colors.SlateBlue : Colors.Gray;

        Assert.Equal(expected, sut.Convert(repeat.ToString(), typeof(Color), null!, Cul));
    }

    [Fact]
    public void ConvertBack_throws()
    {
        var sut = new RepeatColorConverter();

        Assert.Throws<NotImplementedException>(() =>
            sut.ConvertBack(Colors.Gray, typeof(bool), null!, Cul));
    }

    [Fact]
    public void Convert_invalid_bool_throws_format_exception()
    {
        var sut = new RepeatColorConverter();

        Assert.Throws<FormatException>(() =>
            sut.Convert("maybe", typeof(Color), null!, Cul));
    }
}
