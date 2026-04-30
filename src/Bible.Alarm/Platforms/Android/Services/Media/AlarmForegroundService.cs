#nullable enable

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Media;

/// <summary>
/// Foreground service for alarm handling. Prevents OS from killing the app
/// during the delay between alarm trigger and playback start.
/// </summary>
[Service(Exported = true, ForegroundServiceType = ForegroundService.TypeMediaPlayback)]
public class AlarmForegroundService : Service
{
    private static readonly ILogger logger = Log.ForContext<AlarmForegroundService>();
    private static readonly object instanceLock = new();
    private static AlarmForegroundService? instance;
    private CancellationTokenSource? safetyTimeoutCts;

    private const int SafetyTimeoutSeconds = 90;

    /// <summary>
    /// Gets the current service instance in a thread-safe manner.
    /// </summary>
    public static AlarmForegroundService? Instance
    {
        get
        {
            lock (instanceLock)
            {
                return instance;
            }
        }
    }

    private static void SetInstanceReference(AlarmForegroundService? svc)
    {
        lock (instanceLock)
        {
            instance = svc;
        }
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnCreate()
    {
        // CRITICAL: Start foreground with a minimal notification immediately.
        // The coordinator will replace this with a proper alarm notification later,
        // but this ensures the OS doesn't kill the process in the meantime.
        ForegroundServiceOperations.StartForegroundMinimal(this);

        base.OnCreate();
        SetInstanceReference(this);
        logger.Information("AlarmForegroundService.OnCreate() called - instance registered");

        StartSafetyTimeout();
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        logger.Information("AlarmForegroundService.OnStartCommand() called");
        return StartCommandResult.NotSticky;
    }

    public override void OnDestroy()
    {
        logger.Information("AlarmForegroundService.OnDestroy() called");
        CancelSafetyTimeout();
        SetInstanceReference(null);
        base.OnDestroy();
    }

    private void StartSafetyTimeout()
    {
        safetyTimeoutCts = new CancellationTokenSource();
        var token = safetyTimeoutCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(SafetyTimeoutSeconds), token);
                if (!token.IsCancellationRequested)
                {
                    logger.Warning("AlarmForegroundService safety timeout ({TimeoutSeconds}s) - auto-stopping to prevent stuck notification", SafetyTimeoutSeconds);
                    ForegroundServiceCoordinator.StopAlarmForegroundServiceIfActive();
                }
            }
            catch (System.OperationCanceledException)
            {
                // Expected when AlarmForegroundService CTS is cancelled during normal stop.
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error in AlarmForegroundService safety timeout");
            }
        }, token);
    }

    private void CancelSafetyTimeout()
    {
        try
        {
            safetyTimeoutCts?.Cancel();
            safetyTimeoutCts?.Dispose();
            safetyTimeoutCts = null;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error cancelling safety timeout");
        }
    }
}
