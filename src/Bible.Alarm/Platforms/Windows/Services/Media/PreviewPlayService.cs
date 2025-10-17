using Bible.Alarm.Services.Contracts;
using System;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Bible.Alarm.Services.Windows
{
    public class PreviewPlayService : IPreviewPlayService
    {
        private MediaPlayer _mediaPlayer;
        private TaskCompletionSource<bool> _tcs;
        public PreviewPlayService(MediaPlayer player)
        {
            _mediaPlayer = player;
            _mediaPlayer.MediaEnded += MediaEndHandler;
            _mediaPlayer.CurrentStateChanged += BufferingStartedHandler;
        }

        private void MediaEndHandler(MediaPlayer sender, object args)
        {
            OnStopped?.Invoke();
        }

        private void BufferingStartedHandler(MediaPlayer sender, object args)
        {
            if (sender.PlaybackSession.PlaybackState == MediaPlaybackState.Buffering ||
               sender.PlaybackSession.PlaybackState == MediaPlaybackState.Opening
                || sender.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
            {
                if (_tcs.Task.Status == TaskStatus.Running
                    || _tcs.Task.Status == TaskStatus.WaitingForActivation
                    || _tcs.Task.Status == TaskStatus.Created)
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
