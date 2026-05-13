#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class CollectionViewValidityPrecheckTests
{
    [Fact]
    public void HasRenderableItemsSourceWithHandler_returns_false_when_items_source_is_null()
    {
        Assert.False(CollectionViewValidityPrecheck.HasRenderableItemsSourceWithHandler(
            itemsSource: null,
            handlerAttached: true,
            item: new object()));
    }

    [Fact]
    public void HasRenderableItemsSourceWithHandler_returns_false_when_collection_has_no_items()
    {
        var items = new List<object>();
        Assert.False(CollectionViewValidityPrecheck.HasRenderableItemsSourceWithHandler(items, handlerAttached: true, new object()));
    }

    [Fact]
    public void HasRenderableItemsSourceWithHandler_requires_handler_flag_before_accepting_nonempty_source()
    {
        var marker = new object();
        var items = new List<object> { marker };
        Assert.False(CollectionViewValidityPrecheck.HasRenderableItemsSourceWithHandler(items, handlerAttached: false, marker));
    }

    [Fact]
    public void HasRenderableItemsSourceWithHandler_returns_true_when_nonempty_contains_item_and_handler_attached()
    {
        var marker = new object();
        var items = new List<object> { marker };
        Assert.True(CollectionViewValidityPrecheck.HasRenderableItemsSourceWithHandler(items, handlerAttached: true, marker));
    }
}
