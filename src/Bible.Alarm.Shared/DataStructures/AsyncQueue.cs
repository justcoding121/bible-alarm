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
    //data queue.
    private readonly Queue<T> queue = new();

    //consumer task queue and lock.
    private readonly Queue<TaskCompletionSource<T>> consumerQueue = new();
    private readonly SemaphoreSlim consumerQueueLock = new(1);
    private bool disposed;

    public int Count => queue.Count;

    /// <summary>
    ///     Supports multi-threaded producers.
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
    ///      Supports multi-threaded consumers.
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
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(AsyncQueue<T>));
            }

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
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(AsyncQueue<T>));
            }

            if (queue.Count == 0)
            {
                return default;
            }

            return queue.Peek();
        }
        finally
        {
            consumerQueueLock.Release();
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(AsyncQueue<T>));
        }
    }

    public void Dispose()
    {
        if (!disposed)
        {
            // Cancel all pending consumers
            while (consumerQueue.Count > 0)
            {
                var consumer = consumerQueue.Dequeue();
                consumer.TrySetCanceled();
            }

            consumerQueueLock?.Dispose();
            disposed = true;
        }
    }
}
