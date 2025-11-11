using Windows.Media.Core;
using Windows.Media.Playback;
using Bible.Alarm.Common.Interfaces.Media;

namespace Bible.Alarm.Platforms.Windows.Services.Media
{
    public class PreviewPlayService : IPreviewPlayService
    {
        private readonly MediaPlayer _mediaPlayer;
        private TaskCompletionSource<bool> _tcs;

        public PreviewPlayService(MediaPlayer player)
        {
            _mediaPlayer = player;
            
            // Configure audio category for proper playback
            _mediaPlayer.AudioCategory = MediaPlayerAudioCategory.Media;
            
            _mediaPlayer.MediaEnded += MediaEndHandler;
            _mediaPlayer.CurrentStateChanged += BufferingStartedHandler;
        }

        private void MediaEndHandler(MediaPlayer sender, object args)
        {
            OnStopped?.Invoke();
        }

        private void BufferingStartedHandler(MediaPlayer sender, object args)
        {
            if (sender.PlaybackSession.PlaybackState is MediaPlaybackState.Buffering or MediaPlaybackState.Opening or MediaPlaybackState.Playing)
            {
                if (_tcs.Task.Status is TaskStatus.Running or TaskStatus.WaitingForActivation or TaskStatus.Created)
                {
                    _tcs.SetResult(true);
                }
            }
        }

        public event Action OnStopped;

        public async Task Play(string url)
        {
            _tcs = new TaskCompletionSource<bool>();

            var manifestUri = new Uri(url);
            _mediaPlayer.Source = MediaSource.CreateFromUri(manifestUri);
            _mediaPlayer.Play();

            await _tcs.Task;
        }

        public void Stop()
        {
            _mediaPlayer.Pause();
        }

        public void Dispose()
        {
            _mediaPlayer.MediaEnded -= MediaEndHandler;
            _mediaPlayer.BufferingStarted -= BufferingStartedHandler;
            _mediaPlayer.Dispose();
        }
    }
}