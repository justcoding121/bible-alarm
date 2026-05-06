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

    [Fact]
    public void Text_reflects_value_property_changes()
    {
        var sut = new NumberOfTracksListViewItemModel(3, "track", "tracks");

        sut.Value = 1;

        Assert.Equal("1 track", sut.Text);
    }

    [Fact]
    public void IsSelected_and_IsNavigating_setters_update()
    {
        var sut = new NumberOfTracksListViewItemModel(1, "a", "as");

        sut.IsSelected = true;
        sut.IsNavigating = true;

        Assert.True(sut.IsSelected);
        Assert.True(sut.IsNavigating);
    }

    [Fact]
    public void CompareTo_null_item_is_greater()
    {
        var a = new NumberOfTracksListViewItemModel(2, "x", "xs");

        Assert.Equal(1, a.CompareTo((NumberOfTracksListViewItemModel?)null));
        Assert.Equal(1, ((IComparable)a).CompareTo(null));
    }

    [Fact]
    public void CompareTo_object_non_item_delegates_like_null_other()
    {
        var a = new NumberOfTracksListViewItemModel(3, "x", "xs");

        Assert.Equal(1, ((IComparable)a).CompareTo(new object()));
    }

    [Fact]
    public void Equals_object_non_item_returns_false()
    {
        var a = new NumberOfTracksListViewItemModel(4, "x", "xs");

        Assert.False(a.Equals("other"));
    }

    [Fact]
    public void Equality_uses_value_only()
    {
        var x = new NumberOfTracksListViewItemModel(7, "a", "as");
        var y = new NumberOfTracksListViewItemModel(7, "b", "bs");

        Assert.True(x == y);
        Assert.False(x != y);
        Assert.True(x.Equals(y));
        Assert.Equal(x.GetHashCode(), y.GetHashCode());
    }

    [Fact]
    public void Operators_when_left_value_is_greater()
    {
        var left = new NumberOfTracksListViewItemModel(10, "x", "xs");
        var right = new NumberOfTracksListViewItemModel(2, "x", "xs");

        Assert.True(left > right);
        Assert.False(left < right);
        Assert.True(left >= right);
        Assert.False(left <= right);
    }
}
