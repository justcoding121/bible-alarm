#nullable enable

using Bible.Alarm.Common.Helpers;

namespace Bible.Alarm.Tests;

public sealed class BootstrapTimingHelperTests
{
    [Fact]
    public void GetElapsedMilliseconds_is_non_negative()
    {
        Assert.True(BootstrapTimingHelper.GetElapsedMilliseconds() >= 0);
    }

    [Fact]
    public void TimestampToMilliseconds_matches_stopwatch_frequency()
    {
        var a = BootstrapTimingHelper.GetTimestamp();
        Thread.Sleep(15);
        var b = BootstrapTimingHelper.GetTimestamp();

        var ms = BootstrapTimingHelper.TimestampToMilliseconds(a, b);

        Assert.InRange(ms, 5, 2000);
    }
}
