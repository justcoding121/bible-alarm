#nullable enable

using System.Globalization;
using System.Reflection;
using Bible.Alarm.Common.ViewHelpers.Converters;

namespace Bible.Alarm.Tests;

public sealed class ItemTappedEventArgsConverterTests
{
    private static readonly CultureInfo Cul = CultureInfo.InvariantCulture;

    private static SelectionChangedEventArgs CreateSelectionChanged(object? previousSingle, object? currentSingle)
    {
        var ctor = typeof(SelectionChangedEventArgs).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(object), typeof(object)],
            modifiers: null);

        return (SelectionChangedEventArgs)ctor!.Invoke([previousSingle, currentSingle]);
    }

    [Fact]
    public void Convert_selection_changed_uses_first_current_item()
    {
        var sut = new ItemTappedEventArgsConverter();
        var picked = new object();
        var args = CreateSelectionChanged(previousSingle: null, currentSingle: picked);

        Assert.Same(picked, sut.Convert(args, typeof(object), null, Cul));
    }

    [Fact]
    public void Convert_selection_changed_empty_selection_returns_null()
    {
        var sut = new ItemTappedEventArgsConverter();
        var args = CreateSelectionChanged(null, null);

        Assert.Null(sut.Convert(args, typeof(object), null, Cul));
    }

    [Fact]
    public void Convert_item_tapped_returns_item()
    {
        var sut = new ItemTappedEventArgsConverter();
        var item = new object();
        var args = new ItemTappedEventArgs(/* group: */ null, item, /* itemIndex: */ 0);

        Assert.Same(item, sut.Convert(args, typeof(object), null, Cul));
    }

    [Fact]
    public void Convert_unexpected_type_throws()
    {
        var sut = new ItemTappedEventArgsConverter();

        Assert.Throws<ArgumentException>(() => sut.Convert(123, typeof(object), null, Cul));
    }

    [Fact]
    public void ConvertBack_throws()
    {
        var sut = new ItemTappedEventArgsConverter();

        Assert.Throws<NotImplementedException>(() =>
            sut.ConvertBack(null!, typeof(object), null, Cul));
    }
}
