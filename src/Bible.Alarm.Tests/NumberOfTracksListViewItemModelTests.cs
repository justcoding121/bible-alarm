#nullable enable

using Bible.Alarm.ViewModels.Shared;

namespace Bible.Alarm.Tests;

public sealed class NumberOfTracksListViewItemModelTests
{
    [Fact]
    public void Text_uses_singular_when_value_is_one()
    {
        var sut = new NumberOfTracksListViewItemModel(1, "track", "tracks");

        Assert.Equal("1 track", sut.Text);
    }

    [Fact]
    public void Text_uses_plural_when_value_not_one()
    {
        var sut = new NumberOfTracksListViewItemModel(3, "track", "tracks");

        Assert.Equal("3 tracks", sut.Text);
    }

    [Fact]
    public void UpdateUnitLabels_refreshes_text_without_changing_value()
    {
        var sut = new NumberOfTracksListViewItemModel(2, "chapter", "chapters");

        sut.UpdateUnitLabels("book", "books");

        Assert.Equal(2, sut.Value);
        Assert.Equal("2 books", sut.Text);
    }

    [Fact]
    public void CompareTo_and_operators_order_by_value()
    {
        var a = new NumberOfTracksListViewItemModel(1, "x", "xs");
        var b = new NumberOfTracksListViewItemModel(5, "x", "xs");

        Assert.True(a < b);
        Assert.True(b > a);
        Assert.True(a <= b);
        Assert.True(b >= a);
        Assert.NotEqual(a, b);
        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
    }
}
