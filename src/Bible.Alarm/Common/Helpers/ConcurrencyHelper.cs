#nullable enable
using Serilog;

namespace Bible.Alarm.Common.Helpers;

/// <summary>
/// Helper class for executing code within SemaphoreSlim locks, hiding the try/finally pattern.
/// </summary>
public static class ConcurrencyHelper
{
    private const string LogMessageSemaphoreReleaseDisposed = "ObjectDisposedException while releasing SemaphoreSlim lock";

    /// <summary>
    /// Executes an async action within a SemaphoreSlim lock with a timeout, automatically releasing the lock in a finally block if acquired.
    /// </summary>
    public static async Task<bool> ExecuteAsync(SemaphoreSlim @lock, Func<Task> action, int timeoutMs)
    {
        if (await @lock.WaitAsync(timeoutMs))
        {
            try
            {
                await action();
                return true;
            }
            finally
            {
                @lock.Release();
            }
        }

        return false;
    }

    /// <summary>
    /// Executes an async function within a SemaphoreSlim lock with a timeout, automatically releasing the lock in a finally block if acquired.
    /// </summary>
    public static async Task<T?> ExecuteAsync<T>(SemaphoreSlim @lock, Func<Task<T>> func, int timeoutMs)
    {
        if (await @lock.WaitAsync(timeoutMs))
        {
            try
            {
                return await func();
            }
            finally
            {
                @lock.Release();
            }
        }

        return default;
    }

    /// <summary>
    /// Executes an async action within a SemaphoreSlim lock with cancellation token support, automatically releasing the lock in a finally block.
    /// Handles ObjectDisposedException gracefully when releasing the lock.
    /// </summary>
    public static async Task ExecuteAsync(SemaphoreSlim @lock, Func<Task> action, Action<ObjectDisposedException>? onDisposedException = null, CancellationToken cancellationToken = default)
    {
        await @lock.WaitAsync(cancellationToken);
        try
        {
            await action();
        }
        finally
        {
            try
            {
                @lock.Release();
            }
            catch (ObjectDisposedException ex)
            {
                Log.Logger.Debug(ex, LogMessageSemaphoreReleaseDisposed);
                onDisposedException?.Invoke(ex);
            }
        }
    }

    /// <summary>
    /// Executes an async function within a SemaphoreSlim lock with cancellation token support, automatically releasing the lock in a finally block.
    /// Handles ObjectDisposedException gracefully when releasing the lock.
    /// </summary>
    public static async Task<T> ExecuteAsync<T>(SemaphoreSlim @lock, Func<Task<T>> func, Action<ObjectDisposedException>? onDisposedException = null, CancellationToken cancellationToken = default)
    {
        await @lock.WaitAsync(cancellationToken);
        try
        {
            return await func();
        }
        finally
        {
            try
            {
                @lock.Release();
            }
            catch (ObjectDisposedException ex)
            {
                Log.Logger.Debug(ex, LogMessageSemaphoreReleaseDisposed);
                onDisposedException?.Invoke(ex);
            }
        }
    }

    /// <summary>
    /// Like <see cref="ExecuteAsync{T}(SemaphoreSlim, Func{Task{T}}, int)"/> but <typeparamref name="TResult"/> must be a struct so a timed-out wait returns <c>null</c> unambiguously (nullable value semantics).
    /// </summary>
    public static async Task<TResult?> ExecuteWithTimeoutAsync<TResult>(
        SemaphoreSlim @lock,
        Func<Task<TResult>> func,
        int timeoutMs) where TResult : struct
    {
        if (!await @lock.WaitAsync(timeoutMs))
        {
            return null;
        }

        try
        {
            return await func();
        }
        finally
        {
            @lock.Release();
        }
    }
}

