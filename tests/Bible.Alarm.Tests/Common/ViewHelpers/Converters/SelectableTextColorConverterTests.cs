#nullable enable

using System.Globalization;
using Bible.Alarm.Common.ViewHelpers.Converters;

namespace Bible.Alarm.Tests;

public sealed class SelectableTextColorConverterTests
{
    private static readonly CultureInfo Cul = CultureInfo.InvariantCulture;

    [Fact]
    public void Convert_without_application_resources_returns_purple_fallback()
    {
        var sut = new SelectableTextColorConverter();

        var result = sut.Convert(false, typeof(Color), null!, Cul);

        Assert.Equal(Colors.Purple, result);
    }

    [Fact]
    public void ConvertBack_throws()
    {
        var sut = new SelectableTextColorConverter();

        Assert.Throws<NotImplementedException>(() =>
            sut.ConvertBack(Colors.Purple, typeof(Color), null!, Cul));
    }
}
