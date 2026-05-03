using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class TrackCodeComparerTests
{
    [Fact]
    public void Compare_SortsNumericallyWhenBothParseAsInts()
    {
        var cmp = TrackCodeComparer.Comparer;
        Assert.True(cmp.Compare("2", "10") < 0);
        Assert.True(cmp.Compare("10", "3") > 0);
    }

    [Fact]
    public void Compare_StringFallback_IsOrdinalIgnoreCase()
    {
        var cmp = TrackCodeComparer.Comparer;
        Assert.Equal(0, cmp.Compare("Jw", "jW"));
    }
}
