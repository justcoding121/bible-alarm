using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Bible.Alarm.Shared.DataStructures;

/// <summary>
///     A simple asynchronous multi-thread supporting producer/consumer FIFO queue with minimal locking.
/// </summary>
public sealed class AsyncQueue<T> : IDisposable
{
    private readonly Queue<T> queue = new();

    private readonly Queue<TaskCompletionSource<T>> consumerQueue = new();
    private readonly SemaphoreSlim consumerQueueLock = new(1);
    private bool disposed;

    public int Count => queue.Count;

    /// <summary>
    ///     Supports multithreaded producers.
    ///     Time complexity: O(1).
    /// </summary>
    public async Task EnqueueAsync(T value, int millisecondsTimeout = int.MaxValue,
        CancellationToken taskCancellationToken = default)
    {
        ThrowIfDisposed();

        await consumerQueueLock.WaitAsync(millisecondsTimeout, taskCancellationToken);

        try
        {
            if (disposed)
            {
                return;
            }

            if (consumerQueue.Count > 0)
            {
                var consumer = consumerQueue.Dequeue();
                consumer.TrySetResult(value);
            }
            else
            {
                queue.Enqueue(value);
            }
        }
        finally
        {
            consumerQueueLock.Release();
        }
    }

    /// <summary>
    ///      Supports multithreaded consumers.
    ///      Time complexity: O(1).
    /// </summary>
    public async Task<T> DequeueAsync(int millisecondsTimeout = int.MaxValue,
        CancellationToken taskCancellationToken = default)
    {
        ThrowIfDisposed();

        await consumerQueueLock.WaitAsync(millisecondsTimeout, taskCancellationToken);

        TaskCompletionSource<T> consumer;

        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            if (queue.Count > 0)
            {
                var result = queue.Dequeue();
                return result;
            }

            consumer = new TaskCompletionSource<T>();
            taskCancellationToken.Register(() => consumer.TrySetCanceled());
            consumerQueue.Enqueue(consumer);
        }
        finally
        {
            consumerQueueLock.Release();
        }

        return await consumer.Task;
    }

    public async Task<T> PeekAsync()
    {
        ThrowIfDisposed();

        await consumerQueueLock.WaitAsync();

        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return queue.Count == 0 ? default : queue.Peek();
        }
        finally
        {
            consumerQueueLock.Release();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        while (consumerQueue.Count > 0)
        {
            var consumer = consumerQueue.Dequeue();
            consumer.TrySetCanceled();
        }

        consumerQueueLock.Dispose();
        disposed = true;
    }
}
