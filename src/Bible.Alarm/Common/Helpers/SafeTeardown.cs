#nullable enable

namespace Bible.Alarm.Common.Helpers;

/// <summary>
/// Best-effort cancellation and disposal for UI and bootstrap teardown paths where races are expected.
/// </summary>
public static class SafeTeardown
{
    /// <summary>
    /// Cancels the token source; ignores <see cref="ObjectDisposedException"/> when the CTS is already disposed.
    /// </summary>
    public static async ValueTask CancelAsyncNoThrow(CancellationTokenSource cts)
    {
        try
        {
            await cts.CancelAsync();
        }
        catch (ObjectDisposedException)
        {
            // CancelAsync may observe disposal if caller already disposed the CTS.
        }
    }

    /// <summary>
    /// Cancels (when not already cancelled) then disposes; ignores concurrent disposal.
    /// </summary>
    public static void CancelDisposeNoThrow(CancellationTokenSource? cts)
    {
        if (cts is null)
        {
            return;
        }

        try
        {
            if (!cts.IsCancellationRequested)
            {
                cts.Cancel();
            }
        }
        catch (ObjectDisposedException)
        {
            // CTS may already be disposed during concurrent teardown.
        }

        try
        {
            cts.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Concurrent dispose is expected on rapid navigation / cancel.
        }
    }
}
