namespace Advanced.Algorithms.Distributed;

/// <summary>
///     A simple asynchronous multi-thread supporting producer/consumer FIFO queue with minimal locking.
/// </summary>
public class AsyncQueue<T>
{
    //data queue.
    private readonly Queue<T> _queue = new();

    //consumer task queue and lock.
    private readonly Queue<TaskCompletionSource<T>> _consumerQueue = new();
    private SemaphoreSlim _consumerQueueLock = new(1);

    public int Count => _queue.Count;

    /// <summary>
    ///     Supports multi-threaded producers.
    ///     Time complexity: O(1).
    /// </summary>
    public async Task EnqueueAsync(T value, int millisecondsTimeout = int.MaxValue,
        CancellationToken taskCancellationToken = default)
    {
        await _consumerQueueLock.WaitAsync(millisecondsTimeout, taskCancellationToken);

        if (_consumerQueue.Count > 0)
        {
            var consumer = _consumerQueue.Dequeue();
            consumer.TrySetResult(value);
        }
        else
        {
            _queue.Enqueue(value);
        }

        _consumerQueueLock.Release();
    }

    /// <summary>
    ///      Supports multi-threaded consumers.
    ///      Time complexity: O(1).
    /// </summary>
    public async Task<T> DequeueAsync(int millisecondsTimeout = int.MaxValue,
        CancellationToken taskCancellationToken = default)
    {
        await _consumerQueueLock.WaitAsync(millisecondsTimeout, taskCancellationToken);

        TaskCompletionSource<T> consumer;

        try
        {
            if (_queue.Count > 0)
            {
                var result = _queue.Dequeue();
                return result;
            }

            consumer = new TaskCompletionSource<T>();
            taskCancellationToken.Register(() => consumer.TrySetCanceled());
            _consumerQueue.Enqueue(consumer);
        }
        finally
        {
            _consumerQueueLock.Release();
        }

        return await consumer.Task;
    }

    public async Task<T> PeekAsync()
    {
        await _consumerQueueLock.WaitAsync();

        try
        {
            if (_queue.Count == 0) return default;

            return _queue.Peek();
        }
        finally
        {
            _consumerQueueLock.Release();
        }
    }
}