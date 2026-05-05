#nullable enable

using System.Globalization;
using Bible.Alarm.Common.ViewHelpers.Converters;
using Microsoft.Maui.Controls;

namespace Bible.Alarm.Tests;

public sealed class SelectableTextColorConverterTests
{
    private static readonly CultureInfo Cul = CultureInfo.InvariantCulture;

    [Fact]
    public void Convert_uses_primary_resource_when_available_otherwise_purple()
    {
        var sut = new SelectableTextColorConverter();

        var result = sut.Convert(false, typeof(Color), null!, Cul);

        var color = Assert.IsType<Color>(result);
        if (Application.Current?.Resources.TryGetValue("PrimaryColor", out var primaryObj) is true &&
            primaryObj is Color primary)
        {
            Assert.Equal(primary, color);
        }
        else
        {
            Assert.Equal(Colors.Purple, color);
        }
    }

    [Fact]
    public void ConvertBack_throws()
    {
        var sut = new SelectableTextColorConverter();

        Assert.Throws<NotImplementedException>(() =>
            sut.ConvertBack(Colors.Purple, typeof(Color), null!, Cul));
    }
}
