#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Tests;

public sealed class CodeComparisonHelperBibleAlarmTests
{
    [Fact]
    public void Compare_returns_zero_when_both_null()
    {
        Assert.Equal(0, CodeComparisonHelper.Compare(null, null));
    }

    [Fact]
    public void Compare_orders_null_before_non_null()
    {
        Assert.True(CodeComparisonHelper.Compare(null, "a") < 0);
        Assert.True(CodeComparisonHelper.Compare("a", null) > 0);
    }

    [Fact]
    public void Equals_returns_false_when_exactly_one_side_null()
    {
        Assert.False(CodeComparisonHelper.Equals(null, "x"));
        Assert.False(CodeComparisonHelper.Equals("x", null));
    }
}
