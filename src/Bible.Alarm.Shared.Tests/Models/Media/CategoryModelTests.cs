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
    public void CompareTo_Object_Null_ReturnsGreater()
        => Assert.Equal(1, Cat(code: "A").CompareTo((object?)null));

    [Fact]
    public void CompareTo_Object_BoxedCategory_DelegatesTo_CodeOrdinalComparison()
    {
        var left = Cat(id: 1, code: "Same");
        var right = Cat(id: 99, code: "Same");
        Assert.Equal(0, left.CompareTo((object)right));
        Assert.True(left >= right);
        Assert.True(left <= right);
    }

    [Fact]
    public void CompareTo_UsesOrdinal_CaseSensitive_OnCategoryCodes()
        => Assert.NotEqual(0, Cat(code: "Bible").CompareTo(Cat(id: 3, code: "bible")));

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
    public void ComparisonOperators_ReturnFalse_when_Either_operand_null()
    {
        Category? n = null;
        var c = Cat(code: "Q");
        Assert.False(c < n);
        Assert.False(n < c);
        Assert.False(c > n);
        Assert.False(n > c);
        Assert.False(c <= n);
        Assert.False(n <= c);
        Assert.False(c >= n);
        Assert.False(n >= c);
    }

    [Fact]
    public void Equals_ObjectOverload_RejectsNullAndForeignReference()
    {
        var sut = Cat(code: "D");
        Assert.False(sut.Equals((object?)null));
        Assert.False(sut.Equals("D"));
    }

    [Fact]
    public void Operator_NotEquals_WithDifferentStructuralRules()
    {
        Assert.True(Cat(id: 10, code: "Books") != Cat(id: 11, code: "Books"));
        var u = Cat(id: 0, code: "u");
        var v = Cat(id: 0, code: "u");
        Assert.True(u != v);
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

    [Fact]
    public void GetHashCode_Uses_stable_identifier_hash_when_id_non_zero()
    {
        var x = Cat(id: 602, code: "Bible");
        var sameIdDifferentCode = Cat(id: 602, code: "Other");

        Assert.Equal(x.GetHashCode(), sameIdDifferentCode.GetHashCode());
        Assert.True(x.Equals(sameIdDifferentCode));
    }
}
