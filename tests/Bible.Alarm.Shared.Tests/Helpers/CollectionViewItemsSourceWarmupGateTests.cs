#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class CollectionViewItemsSourceWarmupGateTests
{
    [Fact]
    public void ShouldYieldOnceBeforePolling_is_true_when_items_source_is_null()
    {
        Assert.True(CollectionViewItemsSourceWarmupGate.ShouldYieldOnceBeforePolling(null, new object()));
    }

    [Fact]
    public void ShouldYieldOnceBeforePolling_is_true_when_item_not_present_in_source()
    {
        var items = new List<object> { new object() };
        Assert.True(CollectionViewItemsSourceWarmupGate.ShouldYieldOnceBeforePolling(items, new object()));
    }

    [Fact]
    public void ShouldYieldOnceBeforePolling_is_false_when_item_is_already_present()
    {
        var marker = new object();
        var items = new List<object> { marker };
        Assert.False(CollectionViewItemsSourceWarmupGate.ShouldYieldOnceBeforePolling(items, marker));
    }
}
