#nullable enable
namespace Bible.Alarm.Common.Helpers;

/// <summary>
/// Helper class for executing code within SemaphoreSlim locks, hiding the try/finally pattern.
/// </summary>
public static class ConcurrencyHelper
{
    /// <summary>
    /// Executes an async action within a SemaphoreSlim lock, automatically releasing the lock in a finally block.
    /// </summary>
    /// <param name="lock">The SemaphoreSlim to acquire and release</param>
    /// <param name="action">The async action to execute</param>
    public static async Task ExecuteAsync(SemaphoreSlim @lock, Func<Task> action)
    {
        await @lock.WaitAsync();
        try
        {
            await action();
        }
        finally
        {
            @lock.Release();
        }
    }

    /// <summary>
    /// Executes an async function within a SemaphoreSlim lock, automatically releasing the lock in a finally block.
    /// </summary>
    /// <typeparam name="T">The return type</typeparam>
    /// <param name="lock">The SemaphoreSlim to acquire and release</param>
    /// <param name="func">The async function to execute</param>
    /// <returns>The result of the function</returns>
    public static async Task<T> ExecuteAsync<T>(SemaphoreSlim @lock, Func<Task<T>> func)
    {
        await @lock.WaitAsync();
        try
        {
            return await func();
        }
        finally
        {
            @lock.Release();
        }
    }

    /// <summary>
    /// Executes an async action within a SemaphoreSlim lock with a timeout, automatically releasing the lock in a finally block if acquired.
    /// </summary>
    /// <param name="lock">The SemaphoreSlim to acquire and release</param>
    /// <param name="action">The async action to execute</param>
    /// <param name="timeoutMs">Timeout in milliseconds</param>
    /// <returns>True if the lock was acquired and the action executed, false if timeout occurred</returns>
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
    /// <typeparam name="T">The return type</typeparam>
    /// <param name="lock">The SemaphoreSlim to acquire and release</param>
    /// <param name="func">The async function to execute</param>
    /// <param name="timeoutMs">Timeout in milliseconds</param>
    /// <returns>The result of the function if lock was acquired, or default(T) if timeout occurred</returns>
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
    /// Executes an async action within a SemaphoreSlim lock, automatically releasing the lock in a finally block.
    /// Handles ObjectDisposedException gracefully when releasing the lock.
    /// </summary>
    /// <param name="lock">The SemaphoreSlim to acquire and release</param>
    /// <param name="action">The async action to execute</param>
    /// <param name="onDisposedException">Optional callback to handle ObjectDisposedException when releasing the lock</param>
    public static async Task ExecuteAsync(SemaphoreSlim @lock, Func<Task> action, Action<ObjectDisposedException>? onDisposedException = null)
    {
        await @lock.WaitAsync();
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
                onDisposedException?.Invoke(ex);
            }
        }
    }

    /// <summary>
    /// Executes an async function within a SemaphoreSlim lock, automatically releasing the lock in a finally block.
    /// Handles ObjectDisposedException gracefully when releasing the lock.
    /// </summary>
    /// <typeparam name="T">The return type</typeparam>
    /// <param name="lock">The SemaphoreSlim to acquire and release</param>
    /// <param name="func">The async function to execute</param>
    /// <param name="onDisposedException">Optional callback to handle ObjectDisposedException when releasing the lock</param>
    /// <returns>The result of the function</returns>
    public static async Task<T> ExecuteAsync<T>(SemaphoreSlim @lock, Func<Task<T>> func, Action<ObjectDisposedException>? onDisposedException = null)
    {
        await @lock.WaitAsync();
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
                onDisposedException?.Invoke(ex);
            }
        }
    }
}

