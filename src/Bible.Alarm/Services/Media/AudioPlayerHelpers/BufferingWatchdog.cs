#nullable enable
using Serilog;

namespace Bible.Alarm.Services.Media.AudioPlayerHelpers;

/// <summary>
/// Detects when the media player is stuck in a buffering state and triggers recovery.
/// ExoPlayer (and AVPlayer) can remain in STATE_BUFFERING indefinitely on flaky mobile
/// networks without ever firing an error. This watchdog fires a callback after a
/// configurable timeout so the app can attempt recovery via the existing MediaFailed
/// pipeline (CDN probe, URL refresh, retry, and finally error modal with Retry button).
/// </summary>
public sealed partial class BufferingWatchdog : IDisposable
{
    private readonly ILogger logger;
    private readonly Action onStallDetected;
    private readonly TimeSpan stallTimeout;

    private CancellationTokenSource? watchdogCts;
    private volatile bool isRunning;
    private volatile bool isDisposed;

    public BufferingWatchdog(ILogger logger, Action onStallDetected, TimeSpan? stallTimeout = null)
    {
        this.logger = logger;
        this.onStallDetected = onStallDetected;
        this.stallTimeout = stallTimeout ?? TimeSpan.FromSeconds(12);
    }

    /// <summary>
    /// Starts the stall timer. Does nothing if already running so that repeated
    /// buffering-state dispatches don't keep resetting the countdown.
    /// </summary>
    public void OnBufferingStarted()
    {
        if (isDisposed || isRunning)
        {
            return;
        }

        isRunning = true;
        CancelTokenSource();

        watchdogCts = new CancellationTokenSource();
        var token = watchdogCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(stallTimeout, token);

                if (!token.IsCancellationRequested && !isDisposed)
                {
                    logger.Warning(
                        "[BufferingWatchdog] Player stuck in buffering for {Timeout}s — triggering stall recovery",
                        stallTimeout.TotalSeconds);
                    isRunning = false;
                    onStallDetected();
                }
            }
            catch (OperationCanceledException)
            {
                // Buffering resolved normally before timeout
            }
        });
    }

    /// <summary>
    /// Cancels the stall timer (player transitioned to Playing, Paused, Stopped, etc.).
    /// </summary>
    public void Cancel()
    {
        isRunning = false;
        CancelTokenSource();
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        Cancel();
    }

    private void CancelTokenSource()
    {
        try
        {
            watchdogCts?.Cancel();
            watchdogCts?.Dispose();
            watchdogCts = null;
        }
        catch (ObjectDisposedException)
        {
            watchdogCts = null;
        }
    }
}
