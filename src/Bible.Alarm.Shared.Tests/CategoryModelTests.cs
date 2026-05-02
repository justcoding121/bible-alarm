#nullable enable

using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class CategoryModelTests
{
    private static Category Cat(int id = 1, string code = "c") =>
        new() { Id = id, CategoryCode = code };

    [Fact]
    public void CompareTo_Object_NonCategory_ReturnsGreater()
        => Assert.True(Cat(code: "A").CompareTo(new object()) > 0);

    [Fact]
    public void CompareTo_Nulls_And_OrderByOrdinalCategoryCode()
    {
        Assert.Equal(1, Cat(code: "x").CompareTo(null));

        var left = Cat(code: "a");
        var right = Cat(code: "b");
        Assert.True(left.CompareTo(right) < 0);
        Assert.True(left < right);
        Assert.True(left <= right);
        Assert.True(right > left);
        Assert.True(right >= left);
    }

    [Fact]
    public void Equals_Uses_Id_WhenNonZero_OtherwiseReferenceIdentity()
    {
        var sameIdA = Cat(id: 5, code: "Bible");
        var sameIdB = Cat(id: 5, code: "OtherCode");

        Assert.True(sameIdA.Equals(sameIdB));

        var refA = Cat(id: 0, code: "X");
        var refB = refA;
        Assert.True(refA == refB);

        var zeroA = Cat(id: 0, code: "Y");
        var zeroB = Cat(id: 0, code: "Y");
        Assert.False(zeroA.Equals(zeroB));
    }

    [Fact]
    public void GetHashCode_DistinguishesDistinctInstances_WhenIdZero()
    {
        var a = Cat(id: 0, code: "A");
        var b = Cat(id: 0, code: "A");
        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
    }
}
