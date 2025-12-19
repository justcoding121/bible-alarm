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
    private MediaPlayer _player = player;
    private readonly ILogger _logger = logger;

    public event Action OnStopped;

    public void Stop()
    {
        try
        {
            // Only call Stop() if MediaPlayer is in a valid state (Started or Paused)
            // On Android, calling Stop() when in IDLE (state 0) or INITIALIZED (state 1) causes errors
            // Check if player is playing - if it is, it's safe to stop
            // If not playing, it might be in IDLE, INITIALIZED, STOPPED, or ERROR state
            if (_player.IsPlaying)
            {
                _player.Stop();
            }
            // If not playing, the player is already stopped or in an invalid state
            // In that case, just reset it to clear any pending state
            else
            {
                _player.Reset();
            }
        }
        catch (Java.Lang.IllegalStateException ex)
        {
            // Player is in an invalid state (IDLE, INITIALIZED, or ERROR)
            // Just reset it to clear the state
            _logger.Warning(ex, "MediaPlayer is in an invalid state when trying to stop, attempting reset");
            try
            {
                _player.Reset();
            }
            catch (Exception resetEx)
            {
                // Ignore errors during reset - player might already be disposed
                _logger.Debug(resetEx, "Error resetting MediaPlayer during stop, player may already be disposed");
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
            _player.Reset();
            _player.SetOnCompletionListener(this);
            _player.SetDataSource(AndroidApplication.Context, uri);
            _player.Prepare();
            _player.Start();

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error playing preview audio from URL: {Url}", url);
            throw;
        }
    }

    private bool _disposed;

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            // Only call Stop() if MediaPlayer is in a valid state
            if (_player?.IsPlaying == true)
            {
                _player.Stop();
            }
            // Reset to clear any state
            _player?.Reset();
        }
        catch (Exception ex)
        {
            // Ignore errors during disposal - player might already be in an invalid state
            _logger.Warning(ex, "Error stopping or resetting MediaPlayer during disposal, player may already be disposed");
        }
        finally
        {
            _player?.Dispose();
            _player = null;
            _disposed = true;
        }

        base.Dispose(disposing);
    }
}
