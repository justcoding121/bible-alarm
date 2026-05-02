using Bible.Alarm.Shared.DataStructures;

namespace Bible.Alarm.Shared.Tests;

public sealed class AsyncQueueTests
{
    [Fact]
    public async Task DequeueAsync_CompletesWhenEnqueueAsyncRunsAfter()
    {
        using var queue = new AsyncQueue<int>();
        var pending = queue.DequeueAsync();
        await queue.EnqueueAsync(42);
        Assert.Equal(42, await pending);
    }

    [Fact]
    public async Task EnqueueAsync_DeliversInFifoOrder_WhenItemsBuffered()
    {
        using var queue = new AsyncQueue<int>();
        await queue.EnqueueAsync(1);
        await queue.EnqueueAsync(2);
        Assert.Equal(1, await queue.DequeueAsync());
        Assert.Equal(2, await queue.DequeueAsync());
    }

    [Fact]
    public async Task PeekAsync_ReturnsDefault_WhenEmpty()
    {
        using var queue = new AsyncQueue<int>();
        Assert.Equal(0, await queue.PeekAsync());
    }

    [Fact]
    public async Task PeekAsync_ReturnsHeadWithoutRemoving()
    {
        using var queue = new AsyncQueue<int>();
        await queue.EnqueueAsync(7);
        Assert.Equal(7, await queue.PeekAsync());
        Assert.Equal(7, await queue.DequeueAsync());
    }

    [Fact]
    public void Dispose_SecondCall_DoesNotThrow()
    {
        var queue = new AsyncQueue<int>();
        queue.Dispose();
        Assert.Null(Record.Exception(() => queue.Dispose()));
    }

    [Fact]
    public void OperationsAfterDispose_ThrowObjectDisposedException()
    {
        var queue = new AsyncQueue<int>();
        queue.Dispose();
        Assert.Throws<ObjectDisposedException>(() => queue.PeekAsync().GetAwaiter().GetResult());
    }

    [Fact]
    public async Task PendingDequeueCompletesCanceled_WhenDisposed()
    {
        var queue = new AsyncQueue<int>();
        var pending = queue.DequeueAsync();
        queue.Dispose();
        await Assert.ThrowsAnyAsync<TaskCanceledException>(() => pending);
    }
}
