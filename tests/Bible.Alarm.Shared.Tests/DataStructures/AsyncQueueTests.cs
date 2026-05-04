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
    public async Task Count_tracks_buffered_items_not_yet_consumed()
    {
        using var queue = new AsyncQueue<int>();

        Assert.Equal(0, queue.Count);
        await queue.EnqueueAsync(9);
        await queue.EnqueueAsync(8);

        Assert.Equal(2, queue.Count);
        Assert.Equal(9, await queue.DequeueAsync());
        Assert.Equal(1, queue.Count);
    }

    [Fact]
    public async Task DequeueAsync_waiting_consumer_completes_canceled_when_token_pre_canceled()
    {
        using var queue = new AsyncQueue<int>();
        using var cts = new CancellationTokenSource();

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<TaskCanceledException>(() =>
            queue.DequeueAsync(taskCancellationToken: cts.Token));
    }

    [Fact]
    public async Task EnqueueAsync_ThrowsOperationCanceled_WhenTokenCanceledBeforeWait()
    {
        using var queue = new AsyncQueue<int>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            queue.EnqueueAsync(1, taskCancellationToken: cts.Token));
    }

    [Fact]
    public async Task PeekAsync_ReturnsNullWhenEmpty_ForNullableValueType()
    {
        using var queue = new AsyncQueue<int?>();
        Assert.Null(await queue.PeekAsync());
    }

    [Fact]
    public async Task PeekAsync_ReturnsNullWhenEmpty_ForReferenceType()
    {
        using var queue = new AsyncQueue<string>();
        Assert.Null(await queue.PeekAsync());
    }

    [Fact]
    public void Operations_after_Dispose_throw_ObjectDisposedIncluding_EnqueueAsync()
    {
        var queue = new AsyncQueue<int>();
        queue.Dispose();

        Assert.Throws<ObjectDisposedException>(() => queue.PeekAsync().GetAwaiter().GetResult());
        Assert.Throws<ObjectDisposedException>(() => queue.EnqueueAsync(3).GetAwaiter().GetResult());
        Assert.Throws<ObjectDisposedException>(() => queue.DequeueAsync().GetAwaiter().GetResult());
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
