#nullable enable

using System.Globalization;
using Bible.Alarm.Common.ViewHelpers.Converters;
using Microsoft.Maui.Controls;

namespace Bible.Alarm.Tests;

public sealed class ItemTappedEventArgsConverterTests
{
    private static readonly CultureInfo Cul = CultureInfo.InvariantCulture;

    [Fact]
    public void Convert_ItemTapped_returns_Item()
    {
        var sut = new ItemTappedEventArgsConverter();
        var args = new ItemTappedEventArgs(this, "row", 0);

        Assert.Equal("row", sut.Convert(args, typeof(object), null!, Cul));
    }

    [Fact]
    public void Convert_other_type_throws()
    {
        var sut = new ItemTappedEventArgsConverter();

        Assert.Throws<ArgumentException>(() => sut.Convert(42, typeof(object), null!, Cul));
    }

    [Fact]
    public void ConvertBack_throws()
    {
        var sut = new ItemTappedEventArgsConverter();

        Assert.Throws<NotImplementedException>(() => sut.ConvertBack(null, typeof(object), null!, Cul));
    }
}
