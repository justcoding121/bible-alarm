#nullable enable

using AudioToolbox;
using Bible.Alarm.Services.Media.Interfaces;
using Foundation;
using Microsoft.Maui.ApplicationModel;
using Serilog;

namespace Bible.Alarm.Platforms.iOS.Services.Media;

/// <summary>
/// iOS does not expose the user default ringtone URI to apps. Repeats a system alert sound until stopped.
/// </summary>
public sealed class iOSDefaultDeviceRingtoneService(ILogger logger) : IDefaultDeviceRingtoneService
{
    private const double IntervalSeconds = 2.0;

    private readonly object gate = new();
    private NSTimer? timer;

    public void StartLoopingAlarmRingtone()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                lock (gate)
                {
                    timer?.Invalidate();
                    timer = null;
                    PlayAlertOnce();
                    timer = NSTimer.CreateRepeatingScheduledTimer(IntervalSeconds, _ => PlayAlertOnce());
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "iOSDefaultDeviceRingtoneService: failed to start repeating alert");
            }
        });
    }

    public void Stop()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                lock (gate)
                {
                    timer?.Invalidate();
                    timer = null;
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "iOSDefaultDeviceRingtoneService: failed to stop");
            }
        });
    }

    private static void PlayAlertOnce()
    {
        AudioServicesPlaySystemSound(1005);
    }
}
