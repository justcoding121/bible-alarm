#nullable enable

using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Tests;

public sealed class CategoryBibleAlarmTests
{
    [Fact]
    public void CompareTo_object_returns_positive_when_argument_is_not_Category()
    {
        var sut = new Category { CategoryCode = "Z" };

        Assert.True(sut.CompareTo(new object()) > 0);
    }

    [Fact]
    public void CompareTo_object_delegates_when_argument_is_Category()
    {
        var a = new Category { CategoryCode = "Alpha" };
        var b = new Category { CategoryCode = "Beta" };

        Assert.True(a.CompareTo((object)b) < 0);
    }

    [Fact]
    public void CompareTo_Category_orders_by_category_code_ordinal()
    {
        var a = new Category { CategoryCode = "Alpha" };
        var b = new Category { CategoryCode = "Beta" };

        Assert.True(a.CompareTo(b) < 0);
    }

    [Fact]
    public void CompareTo_Category_returns_positive_when_other_is_null()
    {
        var sut = new Category { CategoryCode = "A" };

        Assert.True(sut.CompareTo((Category?)null) > 0);
    }

    [Fact]
    public void Equality_uses_Id_when_both_nonzero()
    {
        var x = new Category { Id = 7, CategoryCode = "A" };
        var y = new Category { Id = 7, CategoryCode = "B" };

        Assert.True(x.Equals(y));
        Assert.True(x == y);
        Assert.False(x != y);
    }

    [Fact]
    public void Equality_uses_reference_when_both_ids_are_zero()
    {
        var x = new Category { CategoryCode = "A" };
        var y = new Category { CategoryCode = "A" };

        Assert.False(x.Equals(y));
        Assert.False(x == y);
        Assert.True(x.Equals(x));
    }

    [Fact]
    public void Equals_object_and_GetHashCode_follow_contract()
    {
        var sut = new Category { Id = 3, CategoryCode = "Bible" };

        Assert.True(sut.Equals((object)sut));
        Assert.Equal(sut.GetHashCode(), new Category { Id = 3, CategoryCode = "Other" }.GetHashCode());
    }

    [Fact]
    public void Relational_operators_follow_comparison_order()
    {
        var low = new Category { CategoryCode = "aa" };
        var high = new Category { CategoryCode = "bb" };

        Assert.True(low < high);
        Assert.True(high > low);
        Assert.True(low <= high);
        Assert.True(high >= low);
    }
}
