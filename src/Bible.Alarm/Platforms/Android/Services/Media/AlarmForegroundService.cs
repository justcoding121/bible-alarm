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

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnCreate()
    {
        base.OnCreate();
        lock (instanceLock)
        {
            instance = this;
        }
        logger.Information("AlarmForegroundService.OnCreate() called - instance registered");
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        logger.Information("AlarmForegroundService.OnStartCommand() called");
        return StartCommandResult.Sticky; // Keep service running
    }

    public override void OnDestroy()
    {
        logger.Information("AlarmForegroundService.OnDestroy() called");
        lock (instanceLock)
        {
            instance = null;
        }
        base.OnDestroy();
    }
}
