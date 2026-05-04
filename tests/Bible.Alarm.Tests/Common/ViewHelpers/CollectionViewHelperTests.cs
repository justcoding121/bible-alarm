#nullable enable

using Bible.Alarm.Common.ViewHelpers;

namespace Bible.Alarm.Tests;

public sealed class CollectionViewHelperTests
{
    [Fact]
    public async Task ScrollToWhenReadyAsync_returns_immediately_when_collection_or_item_null()
    {
        await CollectionViewHelper.ScrollToWhenReadyAsync(null!, new object());
        await CollectionViewHelper.ScrollToWhenReadyAsync(null!, null!);
    }

    [Fact]
    public async Task WaitForNotBusyAsync_false_when_isBusyGetter_null()
    {
        Assert.False(await CollectionViewHelper.WaitForNotBusyAsync(null!));
    }

    [Fact]
    public async Task WaitForNotBusyAsync_true_when_already_not_busy()
    {
        Assert.True(await CollectionViewHelper.WaitForNotBusyAsync(() => false));
    }

    [Fact]
    public async Task WaitForNotBusyAsync_true_after_busy_cleared()
    {
        var latch = false;
        var task = CollectionViewHelper.WaitForNotBusyAsync(() => latch, maxWaitSeconds: 2, delayMs: 20);
        await Task.Delay(60);
        latch = true;

        Assert.True(await task);
    }

    [Fact]
    public async Task WaitForNotBusyAsync_false_when_timeout_before_clear()
    {
        Assert.False(await CollectionViewHelper.WaitForNotBusyAsync(
            () => true,
            maxWaitSeconds: 1,
            delayMs: 50));
    }

    [Fact]
    public async Task WaitForNotBusyAsync_throws_when_canceled_before_success()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CollectionViewHelper.WaitForNotBusyAsync(() => true, 5, 20, cts.Token));
    }
}
