#nullable enable
using Windows.Media.Core;
using Windows.Media.Playback;
using Bible.Alarm.Common.Interfaces.Media;
using Serilog;

namespace Bible.Alarm.Platforms.Windows.Services.Media;

public sealed class WindowsAudioPreviewer : IAudioPreviewer, IDisposable
{
    private readonly MediaPlayer mediaPlayer;
    private readonly ILogger logger;
    private TaskCompletionSource<bool>? tcs;
    private bool disposed;

    public WindowsAudioPreviewer(MediaPlayer player, ILogger logger)
    {
        mediaPlayer = player;
        this.logger = logger;

        // Configure audio category for proper playback
        mediaPlayer.AudioCategory = MediaPlayerAudioCategory.Media;

        mediaPlayer.MediaEnded += MediaEndHandler;
        mediaPlayer.CurrentStateChanged += BufferingStartedHandler;
    }

    private void MediaEndHandler(MediaPlayer sender, object? args) => OnStopped?.Invoke();

    private void BufferingStartedHandler(MediaPlayer sender, object? args)
    {
        try
        {
            if (sender.PlaybackSession.PlaybackState is MediaPlaybackState.Buffering or MediaPlaybackState.Opening or MediaPlaybackState.Playing)
            {
                if (tcs != null && tcs.Task.Status is TaskStatus.Running or TaskStatus.WaitingForActivation or TaskStatus.Created)
                {
                    tcs.SetResult(true);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Error in BufferingStartedHandler");
        }
    }

    public event Action? OnStopped;

    public async Task Play(string url)
    {
        try
        {
            tcs = new TaskCompletionSource<bool>();

            var manifestUri = new Uri(url);
            mediaPlayer.Source = MediaSource.CreateFromUri(manifestUri);
            mediaPlayer.Play();

            await tcs.Task;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error playing preview audio from URL: {Url}", url);
            tcs?.TrySetException(ex);
            throw;
        }
    }

    public void Stop()
    {
        try
        {
            mediaPlayer.Pause();
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Error stopping MediaPlayer, player may already be disposed");
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        try
        {
            mediaPlayer.MediaEnded -= MediaEndHandler;
            mediaPlayer.CurrentStateChanged -= BufferingStartedHandler;
            mediaPlayer.Dispose();
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Error disposing WindowsAudioPreviewer");
        }
        finally
        {
            disposed = true;
        }
    }
}
