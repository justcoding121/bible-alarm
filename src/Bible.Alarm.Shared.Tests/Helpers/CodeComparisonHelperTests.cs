using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class CodeComparisonHelperTests
{
    [Fact]
    public void Equals_True_WhenBothNull()
    {
        Assert.True(CodeComparisonHelper.Equals(null, null));
    }

    [Fact]
    public void Compare_ReturnsZero_WhenBothNull()
    {
        Assert.Equal(0, CodeComparisonHelper.Compare(null, null));
    }

    [Theory]
    [InlineData(null, "a")]
    [InlineData("a", null)]
    public void Equals_False_WhenExactlyOneSideNull(string? a, string? b)
    {
        Assert.False(CodeComparisonHelper.Equals(a, b));
    }

    [Theory]
    [InlineData(null, "b", -1)]
    [InlineData("a", null, 1)]
    public void Compare_OrdersNullBeforeNonNull(string? a, string? b, int sign)
    {
        var c = CodeComparisonHelper.Compare(a, b);
        Assert.True(sign < 0 ? c < 0 : c > 0);
    }

    [Theory]
    [InlineData("1", "01", true)]
    [InlineData("2", "10", false)]
    [InlineData("abc", "ABC", true)]
    public void Equals_UsesNumericVersusOrdinalIgnoreCaseLogic(string a, string b, bool equal)
    {
        Assert.Equal(equal, CodeComparisonHelper.Equals(a, b));
    }

    [Fact]
    public void Compare_ComparesInts_WhenBothParseAsNumbers()
    {
        Assert.True(CodeComparisonHelper.Compare("3", "10") < 0);
        Assert.True(CodeComparisonHelper.Compare("010", "8") > 0);
    }

    [Fact]
    public void Compare_StringFallback_IsOrdinalIgnoreCase()
    {
        Assert.Equal(0, CodeComparisonHelper.Compare("Jw", "jW"));
    }
}
