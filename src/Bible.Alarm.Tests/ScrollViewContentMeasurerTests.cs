#nullable enable

using Bible.Alarm.Common.ViewHelpers;

namespace Bible.Alarm.Tests;

public sealed class ScrollViewContentMeasurerTests
{
    [Fact]
    public async Task Null_scroll_view_returns_false()
    {
        var result = await ScrollViewContentMeasurer.EnsureContentMeasuredAsync(null!, CancellationToken.None);
        Assert.False(result);
    }
}
