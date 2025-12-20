using Android.Media;
using Bible.Alarm.Common.Interfaces.Media;
using Serilog;
using AndroidApplication = Android.App.Application;
using AndroidNet = Android.Net;
using Object = Java.Lang.Object;


namespace Bible.Alarm.Platforms.Android.Services.Media;

public class AndroidAudioPreviewer(MediaPlayer player, ILogger logger) : Object,
    MediaPlayer.IOnCompletionListener, IAudioPreviewer, IDisposable
{
    private MediaPlayer player = player;
    private readonly ILogger logger = logger;

    public event Action OnStopped;

    public void Stop()
    {
        try
        {
            // Only call Stop() if MediaPlayer is in a valid state (Started or Paused)
            // On Android, calling Stop() when in IDLE (state 0) or INITIALIZED (state 1) causes errors
            // Check if player is playing - if it is, it's safe to stop
            // If not playing, it might be in IDLE, INITIALIZED, STOPPED, or ERROR state
            if (player.IsPlaying)
            {
                player.Stop();
            }
            // If not playing, the player is already stopped or in an invalid state
            // In that case, just reset it to clear any pending state
            else
            {
                player.Reset();
            }
        }
        catch (Java.Lang.IllegalStateException ex)
        {
            // Player is in an invalid state (IDLE, INITIALIZED, or ERROR)
            // Just reset it to clear the state
            logger.Warning(ex, "MediaPlayer is in an invalid state when trying to stop, attempting reset");
            try
            {
                player.Reset();
            }
            catch (Exception resetEx)
            {
                // Ignore errors during reset - player might already be disposed
                logger.Debug(resetEx, "Error resetting MediaPlayer during stop, player may already be disposed");
            }
        }
    }

    public void OnCompletion(MediaPlayer mp)
    {
        OnStopped?.Invoke();
    }

    Task IAudioPreviewer.Play(string url)
    {
        try
        {
            var uri = AndroidNet.Uri.Parse(url);
            player.Reset();
            player.SetOnCompletionListener(this);
            player.SetDataSource(AndroidApplication.Context, uri);
            player.Prepare();
            player.Start();

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error playing preview audio from URL: {Url}", url);
            throw;
        }
    }

    private bool disposed;

    protected override void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        try
        {
            // Only call Stop() if MediaPlayer is in a valid state
            if (player?.IsPlaying == true)
            {
                player.Stop();
            }
            // Reset to clear any state
            player?.Reset();
        }
        catch (Exception ex)
        {
            // Ignore errors during disposal - player might already be in an invalid state
            logger.Warning(ex, "Error stopping or resetting MediaPlayer during disposal, player may already be disposed");
        }
        finally
        {
            player?.Dispose();
            player = null;
            disposed = true;
        }

        base.Dispose(disposing);
    }
}
