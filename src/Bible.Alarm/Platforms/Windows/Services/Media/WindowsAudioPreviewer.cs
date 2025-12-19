#nullable enable
using Bible.Alarm.Common.Interfaces.Media;
using Serilog;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Bible.Alarm.Platforms.Windows.Services.Media
{
    public class WindowsAudioPreviewer : IAudioPreviewer, IDisposable
    {
        private readonly MediaPlayer _mediaPlayer;
        private readonly ILogger _logger;
        private TaskCompletionSource<bool>? _tcs;
        private bool _disposed;

        public WindowsAudioPreviewer(MediaPlayer player, ILogger logger)
        {
            _mediaPlayer = player;
            _logger = logger;

            // Configure audio category for proper playback
            _mediaPlayer.AudioCategory = MediaPlayerAudioCategory.Media;

            _mediaPlayer.MediaEnded += MediaEndHandler;
            _mediaPlayer.CurrentStateChanged += BufferingStartedHandler;
        }

        private void MediaEndHandler(MediaPlayer sender, object? args)
        {
            OnStopped?.Invoke();
        }

        private void BufferingStartedHandler(MediaPlayer sender, object? args)
        {
            try
            {
                if (sender.PlaybackSession.PlaybackState is MediaPlaybackState.Buffering or MediaPlaybackState.Opening or MediaPlaybackState.Playing)
                {
                    if (_tcs != null && _tcs.Task.Status is TaskStatus.Running or TaskStatus.WaitingForActivation or TaskStatus.Created)
                    {
                        _tcs.SetResult(true);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Error in BufferingStartedHandler");
            }
        }

        public event Action? OnStopped;

        public async Task Play(string url)
        {
            try
            {
                _tcs = new TaskCompletionSource<bool>();

                var manifestUri = new Uri(url);
                _mediaPlayer.Source = MediaSource.CreateFromUri(manifestUri);
                _mediaPlayer.Play();

                await _tcs.Task;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error playing preview audio from URL: {Url}", url);
                _tcs?.TrySetException(ex);
                throw;
            }
        }

        public void Stop()
        {
            try
            {
                _mediaPlayer.Pause();
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Error stopping MediaPlayer, player may already be disposed");
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _mediaPlayer.MediaEnded -= MediaEndHandler;
                _mediaPlayer.CurrentStateChanged -= BufferingStartedHandler;
                _mediaPlayer.Dispose();
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Error disposing WindowsAudioPreviewer");
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}