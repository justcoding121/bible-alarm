using Bible.Alarm.Cataloger.Models.BiblePublications;

namespace Bible.Alarm.Cataloger.Tests;

public sealed class BiblePublicationSectionTests
{
    private static BiblePublicationSection Row(int number, string name = "") =>
        new()
        {
            Number = number,
            Name = name,
        };

    [Fact]
    public void CompareTo_orders_by_Number()
    {
        var low = Row(3);
        var high = Row(10);

        Assert.True(low < high);
        Assert.True(high > low);
        Assert.Equal(0, low.CompareTo(Row(3, "IgnoredForEquality")));
        Assert.Equal(1, low.CompareTo((BiblePublicationSection?)null));

        Assert.Equal(1, low.CompareTo(new object()));
    }

    [Fact]
    public void Equality_and_HashCode_follow_Number_only()
    {
        var a = Row(42, "A");
        var b = Row(42, "Z");

        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object)b));
        Assert.False(a.Equals(new object()));

        Assert.True(a == b);
        Assert.False(a != b);

        Assert.True(a <= b);
        Assert.True(a >= b);
        Assert.True(a <= Row(43));
        Assert.True(a < Row(100));

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
