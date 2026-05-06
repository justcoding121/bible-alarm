#nullable enable

using System.Collections;
using System.Globalization;
using Bible.Alarm.Common.ViewHelpers.Converters;
using Microsoft.Maui.Controls;

namespace Bible.Alarm.Tests;

public sealed class ItemTappedEventArgsConverterTests
{
    private static readonly CultureInfo Cul = CultureInfo.InvariantCulture;

    private static SelectionChangedEventArgs CreateSelectionChanged(IList previousSelection, IList currentSelection) =>
        (SelectionChangedEventArgs)Activator.CreateInstance(
            typeof(SelectionChangedEventArgs),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            args: [previousSelection, currentSelection],
            culture: null)!;

    [Fact]
    public void Convert_ItemTapped_returns_Item()
    {
        var sut = new ItemTappedEventArgsConverter();
        var args = new ItemTappedEventArgs(this, "row", 0);

        Assert.Equal("row", sut.Convert(args, typeof(object), null!, Cul));
    }

    [Fact]
    public void Convert_SelectionChanged_returns_first_current_item_when_present()
    {
        var sut = new ItemTappedEventArgsConverter();
        var args = CreateSelectionChanged(
            previousSelection: new List<object?>(),
            currentSelection: new List<object?> { 99, 100 });

        Assert.Equal(99, sut.Convert(args, typeof(object), null!, Cul));
    }

    [Fact]
    public void Convert_SelectionChanged_returns_null_when_current_empty()
    {
        var sut = new ItemTappedEventArgsConverter();
        var args = CreateSelectionChanged(
            previousSelection: new List<object?> { "old" },
            currentSelection: new List<object?>());

        Assert.Null(sut.Convert(args, typeof(object), null!, Cul));
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
