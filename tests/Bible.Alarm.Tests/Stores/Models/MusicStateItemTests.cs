#nullable enable

using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class MusicStateItemTests
{
    [Fact]
    public void CompareTo_orders_by_id()
    {
        var low = new MusicStateItem { Id = 1 };
        var high = new MusicStateItem { Id = 9 };

        Assert.True(low.CompareTo(high) < 0);
        Assert.True(high.CompareTo(low) > 0);
        Assert.Equal(0, low.CompareTo(new MusicStateItem { Id = 1 }));
    }

    [Fact]
    public void CompareTo_null_music_item_is_greater()
    {
        var a = new MusicStateItem { Id = 1 };

        Assert.Equal(1, a.CompareTo((MusicStateItem?)null));
        Assert.Equal(1, ((IComparable)a).CompareTo(null));
    }

    [Fact]
    public void Equality_uses_id_only()
    {
        var x = new MusicStateItem { Id = 5, PublicationCode = "a", TrackCode = "1" };
        var y = new MusicStateItem { Id = 5, PublicationCode = "b", TrackCode = "2" };

        Assert.True(x == y);
        Assert.False(x != y);
        Assert.True(x.Equals(y));
        Assert.Equal(x.GetHashCode(), y.GetHashCode());
    }

    [Fact]
    public void Comparison_operators_follow_id_order()
    {
        var a = new MusicStateItem { Id = 2 };
        var b = new MusicStateItem { Id = 7 };

        Assert.True(a < b);
        Assert.False(a > b);
        Assert.True(a <= b);
        Assert.False(a >= b);
    }
}
