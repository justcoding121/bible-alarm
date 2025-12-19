#nullable enable
using Serilog;
#if ANDROID
using Bible.Alarm.Platforms.Android.Services.Helpers;
using Microsoft.Maui.ApplicationModel;
using Android.App;
#elif IOS
using Bible.Alarm.Platforms.iOS.Helpers;
#elif WINDOWS
using Bible.Alarm.Platforms.Windows.Helpers;
#endif

namespace Bible.Alarm.Common.Helpers;

/// <summary>
/// Helper class for managing platform-specific bootstrap initialization.
/// Handles thread-safe bootstrap execution, waiting, and completion signaling.
/// </summary>
public static class BootstrapHelper
{
    private static readonly object BootstrapLock = new object();
    private static volatile bool BootstrapCompleted = false;
    private static readonly object BootstrapWaitLock = new object();
    private static TaskCompletionSource<bool>? _bootstrapCompletionSource;

    /// <summary>
    /// Initializes platform-specific bootstrap.
    /// This should be called after MauiApp is created to ensure databases and services are initialized.
    /// Thread-safe: ensures bootstrap runs only once, even if called from multiple entry points concurrently.
    /// </summary>
    public static void InitializePlatformBootstrap(IServiceProvider services, bool isForeground = false)
    {
        // Log caller information to help debug which code path is calling bootstrap
        var caller = new System.Diagnostics.StackTrace().GetFrame(1)?.GetMethod()?.DeclaringType?.Name ?? "Unknown";
        Log.Logger.Information("InitializePlatformBootstrap called from {Caller} with isForeground={IsForeground}, BootstrapCompleted={BootstrapCompleted}",
            caller, isForeground, BootstrapCompleted);

        if (IsBootstrapCompleted())
        {
            return;
        }

        var lockAcquired = TryAcquireBootstrapLock(isForeground, services);

        if (lockAcquired)
        {
            HandleBootstrapWithLock(services, isForeground);
        }
        else
        {
            HandleBootstrapWithoutLock(services, isForeground);
        }
    }

    /// <summary>
    /// Synchronously waits for bootstrap to complete before allowing database access.
    /// Use this method when you must wait synchronously (e.g., in framework override methods).
    /// </summary>
    public static void WaitForBootstrap(int timeoutMs = 30000)
    {
        if (BootstrapCompleted)
        {
            Log.Logger.Debug("Bootstrap already completed, returning immediately");
            return;
        }

        Log.Logger.Information("Waiting synchronously for bootstrap to complete (timeout: {TimeoutMs}ms)", timeoutMs);

        // Use Task.Run to avoid deadlock issues when called from synchronization contexts
        // Task.Run executes on a thread pool thread (no synchronization context), so ConfigureAwait is not needed
        var waitTask = Task.Run(async () => await WaitForBootstrapAsync(timeoutMs));

        // Wait for the task to complete with timeout to prevent indefinite blocking
        // Use a slightly longer timeout than the async version to account for Task.Run overhead
        if (!waitTask.Wait(timeoutMs + 1000))
        {
            Log.Logger.Warning("WaitForBootstrap timed out after {TimeoutMs}ms - proceeding anyway", timeoutMs);
            // Don't throw - allow code to proceed even if bootstrap wait timed out
            // This prevents Android Auto from hanging indefinitely
        }
        else if (waitTask.IsFaulted)
        {
            Log.Logger.Warning(waitTask.Exception?.GetBaseException(), "WaitForBootstrap encountered an error - proceeding anyway");
            // Don't throw - allow code to proceed even if bootstrap wait failed
        }
    }

    /// <summary>
    /// Waits for bootstrap to complete before allowing database access.
    /// This ensures database migrations are finished before services use the database.
    /// </summary>
    public static async Task WaitForBootstrapAsync(int timeoutMs = 30000)
    {
        if (BootstrapCompleted)
        {
            Log.Logger.Debug("Bootstrap already completed, returning immediately");
            return;
        }

        Log.Logger.Information("Waiting for bootstrap to complete (timeout: {TimeoutMs}ms)", timeoutMs);

        Task<bool> waitTask;
        lock (BootstrapWaitLock)
        {
            // Check again inside lock (bootstrap might have completed while waiting for lock)
            if (BootstrapCompleted)
            {
                Log.Logger.Debug("Bootstrap completed while waiting for lock, returning immediately");
                return;
            }

            // Create or reuse the completion source
            // If bootstrap already completed, the source should already be set
            // If it's null or completed, create a new one (bootstrap is still running)
            if (_bootstrapCompletionSource == null || _bootstrapCompletionSource.Task.IsCompleted)
            {
                // Double-check bootstrap didn't complete between the outer check and now
                if (BootstrapCompleted)
                {
                    Log.Logger.Debug("Bootstrap completed between checks, returning immediately");
                    return;
                }
                _bootstrapCompletionSource = new TaskCompletionSource<bool>();
            }
            waitTask = _bootstrapCompletionSource.Task;
        }

        // Wait for bootstrap completion with timeout
        using var cts = new CancellationTokenSource(timeoutMs);
        var timeoutTask = Task.Delay(timeoutMs, cts.Token).ContinueWith(_ =>
        {
            lock (BootstrapWaitLock)
            {
                if (_bootstrapCompletionSource != null && !_bootstrapCompletionSource.Task.IsCompleted)
                {
                    _bootstrapCompletionSource.TrySetException(new TimeoutException($"Bootstrap did not complete within {timeoutMs}ms"));
                }
            }
        }, TaskContinuationOptions.ExecuteSynchronously);

        try
        {
            await waitTask;
            cts.Cancel(); // Cancel timeout if bootstrap completed
            Log.Logger.Information("Bootstrap wait completed successfully");
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Error waiting for bootstrap completion");
            throw;
        }
    }

    private static bool IsBootstrapCompleted()
    {
        if (BootstrapCompleted)
        {
            Log.Logger.Information("Bootstrap already completed, returning early");
            return true;
        }
        return false;
    }

    private static bool TryAcquireBootstrapLock(bool isForeground, IServiceProvider services)
    {
        // Try to acquire lock without blocking (using Monitor.TryEnter)
        bool lockAcquired = false;
        try
        {
            Monitor.TryEnter(BootstrapLock, 0, ref lockAcquired);
            if (!lockAcquired)
            {
                return false;
            }

            if (IsBootstrapCompleted())
            {
                Log.Logger.Information("Bootstrap completed while waiting for lock, returning");
                return true;
            }

            Log.Logger.Information("Acquired bootstrap lock, isForeground={IsForeground}", isForeground);

            if (isForeground)
            {
                // For foreground, start bootstrap in background task (fire and forget)
                // Lock will be released immediately, bootstrap runs asynchronously
                RunForegroundBootstrap(services);
            }
            else
            {
                // For background services, we'll run bootstrap outside the lock
                // This prevents holding BootstrapLock while VerifyServices waits for its own lock
                Log.Logger.Information("Background service: will run bootstrap outside lock");
            }

            return true;
        }
        finally
        {
            if (lockAcquired)
            {
                Monitor.Exit(BootstrapLock);
            }
        }
    }

    private static void HandleBootstrapWithLock(IServiceProvider services, bool isForeground)
    {
        if (!isForeground)
        {
            // Run bootstrap asynchronously on a background task to avoid blocking
            // This prevents holding BootstrapLock while VerifyServices waits for its lock
            _ = Task.Run(async () =>
            {
                await ExecuteBootstrapWithErrorHandlingAsync(services, isForeground, "in background Task (background service) - outside lock");
            });
        }
        // For foreground, bootstrap already started in background task (fire and forget)
    }

    private static void HandleBootstrapWithoutLock(IServiceProvider services, bool isForeground)
    {
        Log.Logger.Information("Bootstrap lock not acquired, isForeground={IsForeground}", isForeground);

        if (isForeground)
        {
            // For foreground, don't block - just return and let the other bootstrap complete
            Log.Logger.Information("Foreground launch: returning without blocking, letting other bootstrap complete");
            return;
        }

        // For background services, wait for the lock and check if bootstrap completed
        WaitForBootstrapLock(services, isForeground);
    }

    private static void RunForegroundBootstrap(IServiceProvider services)
    {
        // Run bootstrap as a background job for foreground launches to avoid blocking UI
        // Fire and forget - don't await
        _ = Task.Run(async () =>
        {
            await ExecuteBootstrapWithErrorHandlingAsync(services, true, "in background Task (foreground launch)");
        });
    }

    private static void WaitForBootstrapLock(IServiceProvider services, bool isForeground)
    {
        Log.Logger.Information("Background service: waiting for lock");
        lock (BootstrapLock)
        {
            if (IsBootstrapCompleted())
            {
                Log.Logger.Information("Bootstrap completed while waiting for lock");
                return;
            }

            // Previous bootstrap may have failed or was interrupted, continue to run bootstrap
            // Run asynchronously on a background task to avoid blocking
            Log.Logger.Information("Previous bootstrap may have failed, running bootstrap");
            _ = Task.Run(async () =>
            {
                await ExecuteBootstrapWithErrorHandlingAsync(services, isForeground, "in background Task (after waiting for lock)");
            });
        }
    }

    private static async Task ExecuteBootstrapWithErrorHandlingAsync(IServiceProvider services, bool isForeground, string context)
    {
        try
        {
            Log.Logger.Information("Running bootstrap {Context}", context);
            await RunBootstrap(services, isForeground);
            BootstrapCompleted = true;

            // Signal waiting tasks that bootstrap is complete
            // CRITICAL: Always ensure completion source exists and is set, even if no one was waiting
            // This prevents issues where WaitForBootstrap() is called after bootstrap completes
            lock (BootstrapWaitLock)
            {
                if (_bootstrapCompletionSource == null)
                {
                    // Create a completed source for future callers
                    _bootstrapCompletionSource = new TaskCompletionSource<bool>();
                    _bootstrapCompletionSource.TrySetResult(true);
                }
                else if (!_bootstrapCompletionSource.Task.IsCompleted)
                {
                    _bootstrapCompletionSource.TrySetResult(true);
                }
            }

            Log.Logger.Information("Bootstrap completed {Context}", context);
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Error in bootstrap initialization {Context}", context);

            // Signal failure to waiting tasks
            lock (BootstrapWaitLock)
            {
                if (_bootstrapCompletionSource == null)
                {
                    _bootstrapCompletionSource = new TaskCompletionSource<bool>();
                    _bootstrapCompletionSource.TrySetException(ex);
                }
                else if (!_bootstrapCompletionSource.Task.IsCompleted)
                {
                    _bootstrapCompletionSource.TrySetException(ex);
                }
            }
        }
    }

    private static async Task RunBootstrap(IServiceProvider services, bool isForeground)
    {
        try
        {
            var logger = services.GetRequiredService<ILogger>();

#if ANDROID
            // Android bootstrap initialization
            // Try Platform.CurrentActivity first, fallback to AndroidApplication.Context
            var context = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.ApplicationContext ?? Android.App.Application.Context;
            var application = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.Application ?? Android.App.Application.Context as Android.App.Application;

            if (context != null)
            {
                await AndroidBootstrapHelper.Initialize(logger, context, application, isForeground);
            }
            else
            {
                logger.Warning("Android context not available for bootstrap initialization");
            }
#elif IOS
            // iOS bootstrap initialization
            await iOSBootstrapHelper.Initialize(logger, isForeground);
#elif WINDOWS
            // Windows bootstrap initialization
            await WindowsBootstrapHelper.Initialize(logger, isForeground);
#endif
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Error in InitializePlatformBootstrap");
        }
    }
}

