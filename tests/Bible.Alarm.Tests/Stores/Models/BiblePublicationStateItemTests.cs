#nullable enable

using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationStateItemTests
{
    [Fact]
    public void CompareTo_orders_by_id()
    {
        var low = new BiblePublicationStateItem { Id = 2 };
        var high = new BiblePublicationStateItem { Id = 10 };

        Assert.True(low.CompareTo(high) < 0);
        Assert.True(high.CompareTo(low) > 0);
        Assert.Equal(0, low.CompareTo(new BiblePublicationStateItem { Id = 2 }));
    }

    [Fact]
    public void CompareTo_null_item_is_greater()
    {
        var a = new BiblePublicationStateItem { Id = 1 };

        Assert.Equal(1, a.CompareTo((BiblePublicationStateItem?)null));
        Assert.Equal(1, ((IComparable)a).CompareTo(null));
    }

    [Fact]
    public void Equality_uses_id_only()
    {
        var x = new BiblePublicationStateItem { Id = 4, PublicationCode = "a", TrackCode = "1" };
        var y = new BiblePublicationStateItem { Id = 4, PublicationCode = "b", TrackCode = "9" };

        Assert.True(x == y);
        Assert.False(x != y);
        Assert.True(x.Equals(y));
        Assert.Equal(x.GetHashCode(), y.GetHashCode());
    }

    [Fact]
    public void Comparison_operators_follow_id_order()
    {
        var a = new BiblePublicationStateItem { Id = 3 };
        var b = new BiblePublicationStateItem { Id = 8 };

        Assert.True(a < b);
        Assert.False(a > b);
        Assert.True(a <= b);
        Assert.False(a >= b);
    }
}
