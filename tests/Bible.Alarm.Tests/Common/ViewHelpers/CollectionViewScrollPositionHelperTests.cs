#nullable enable

using Bible.Alarm.Common.ViewHelpers;
using Microsoft.Maui.Controls;

namespace Bible.Alarm.Tests;

public sealed class CollectionViewScrollPositionHelperTests
{
    [Theory]
    [InlineData(0, 5, (int)ScrollToPosition.Start)]
    [InlineData(4, 5, (int)ScrollToPosition.End)]
    [InlineData(1, 6, (int)ScrollToPosition.Center)]
    [InlineData(3, 6, (int)ScrollToPosition.MakeVisible)]
    [InlineData(5, 6, (int)ScrollToPosition.End)]
    public void OptimalScrollPositionForIndexedItem_follows_start_end_last_three_and_center_rules(
        int itemIndex,
        int totalItems,
        int expected)
    {
        var position = CollectionViewScrollPositionHelper.OptimalScrollPositionForIndexedItem(itemIndex, totalItems);

        Assert.Equal((ScrollToPosition)expected, position);
    }

    [Fact]
    public void OptimalScrollPositionForIndexedItem_returns_center_when_index_invalid()
    {
        var position = CollectionViewScrollPositionHelper.OptimalScrollPositionForIndexedItem(-1, 3);

        Assert.Equal(ScrollToPosition.Center, position);
    }
}
