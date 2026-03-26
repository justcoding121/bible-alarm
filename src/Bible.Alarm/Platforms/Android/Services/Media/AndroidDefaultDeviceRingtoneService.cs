#nullable enable

using Android.Content;
using Android.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Microsoft.Maui.ApplicationModel;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Media;

public sealed class AndroidDefaultDeviceRingtoneService(ILogger logger) : IDefaultDeviceRingtoneService
{
    private readonly object gate = new();
    private MediaPlayer? mediaPlayer;

    public void StartLoopingAlarmRingtone()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                lock (gate)
                {
                    ReleasePlayerLocked();

                    Context context = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity
                        ?? global::Android.App.Application.Context;
                    var uri = RingtoneManager.GetDefaultUri(RingtoneType.Ringtone)
                        ?? RingtoneManager.GetDefaultUri(RingtoneType.Notification);
                    if (uri == null)
                    {
                        logger.Warning("AndroidDefaultDeviceRingtoneService: no default ringtone URI");
                        return;
                    }

                    var player = new MediaPlayer();
                    var attrsBuilder = new AudioAttributes.Builder();
                    if (attrsBuilder == null)
                    {
                        logger.Warning("AndroidDefaultDeviceRingtoneService: could not create AudioAttributes.Builder");
                        return;
                    }

                    attrsBuilder.SetUsage(AudioUsageKind.Alarm);
                    attrsBuilder.SetContentType(AudioContentType.Music);
                    var attrs = attrsBuilder.Build();
                    if (attrs == null)
                    {
                        logger.Warning("AndroidDefaultDeviceRingtoneService: could not build AudioAttributes");
                        return;
                    }

                    player.SetAudioAttributes(attrs);
                    player.SetDataSource(context, uri);
                    player.Looping = true;
                    player.Prepare();
                    player.Start();
                    mediaPlayer = player;
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "AndroidDefaultDeviceRingtoneService: failed to start default ringtone");
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
                    ReleasePlayerLocked();
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "AndroidDefaultDeviceRingtoneService: failed to stop ringtone");
            }
        });
    }

    private void ReleasePlayerLocked()
    {
        if (mediaPlayer == null)
        {
            return;
        }

        try
        {
            if (mediaPlayer.IsPlaying)
            {
                mediaPlayer.Stop();
            }
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "AndroidDefaultDeviceRingtoneService: stop before release");
        }

        try
        {
            mediaPlayer.Release();
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "AndroidDefaultDeviceRingtoneService: release");
        }

        mediaPlayer = null;
    }
}
