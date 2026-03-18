#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Serilog;
using Windows.Media.Playback;
using WinMediaSource = Windows.Media.Core.MediaSource;

namespace Bible.Alarm.Platforms.Windows.Services.Media;

public sealed class WindowsDefaultDeviceRingtoneService(ILogger logger) : IDefaultDeviceRingtoneService
{
    private readonly object gate = new();
    private MediaPlayer? player;

    public void StartLoopingAlarmRingtone()
    {
        try
        {
            lock (gate)
            {
                StopLocked();
                var p = new MediaPlayer
                {
                    Source = WinMediaSource.CreateFromUri(new Uri("ms-winsoundevent:Notification.Looping.Alarm")),
                    IsLoopingEnabled = true
                };
                p.Play();
                player = p;
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "WindowsDefaultDeviceRingtoneService: failed to start looping alarm sound");
        }
    }

    public void Stop()
    {
        try
        {
            lock (gate)
            {
                StopLocked();
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "WindowsDefaultDeviceRingtoneService: failed to stop");
        }
    }

    private void StopLocked()
    {
        if (player == null)
        {
            return;
        }

        try
        {
            player.Pause();
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "WindowsDefaultDeviceRingtoneService: pause");
        }

        try
        {
            player.Dispose();
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "WindowsDefaultDeviceRingtoneService: dispose");
        }

        player = null;
    }
}
