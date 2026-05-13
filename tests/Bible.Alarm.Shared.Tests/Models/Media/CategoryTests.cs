#nullable enable

using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class CategoryTests
{
    [Fact]
    public void CompareTo_orders_by_CategoryCode_string_ordinal()
    {
        var a = new Category { CategoryCode = "Alpha" };
        var b = new Category { CategoryCode = "Beta" };

        Assert.True(a.CompareTo(b) < 0);
    }

    [Fact]
    public void CompareTo_object_returns_positive_when_argument_is_not_Category()
    {
        var sut = new Category { CategoryCode = "Z" };

        Assert.True(sut.CompareTo(new object()) > 0);
    }

    [Fact]
    public void Equality_uses_Id_when_both_nonzero()
    {
        var x = new Category { Id = 7, CategoryCode = "A" };
        var y = new Category { Id = 7, CategoryCode = "B" };

        Assert.True(x.Equals(y));
    }
}
