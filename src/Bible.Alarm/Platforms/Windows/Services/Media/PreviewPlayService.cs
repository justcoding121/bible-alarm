using Bible.Alarm.Services.Contracts;
using System;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace JW.Alarm.Services.UWP
{
    public class PreviewPlayService : IPreviewPlayService
    {
        private MediaPlayer mediaPlayer;
        private TaskCompletionSource<bool> tcs;
        public PreviewPlayService(MediaPlayer player)
        {
            this.mediaPlayer = player;
            mediaPlayer.MediaEnded += mediaEndHandler;
            mediaPlayer.CurrentStateChanged += bufferingStartedHandler;
        }

        private void mediaEndHandler(MediaPlayer sender, object args)
        {
            OnStopped?.Invoke();
        }

        private void bufferingStartedHandler(MediaPlayer sender, object args)
        {
            if (sender.PlaybackSession.PlaybackState == MediaPlaybackState.Buffering ||
               sender.PlaybackSession.PlaybackState == MediaPlaybackState.Opening
                || sender.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
            {
                if (tcs.Task.Status == TaskStatus.Running
                    || tcs.Task.Status == TaskStatus.WaitingForActivation
                    || tcs.Task.Status == TaskStatus.Created)
                {
                    tcs.SetResult(true);
                }
            }

        }

        public event Action OnStopped;

        public async Task Play(string url)
        {
            this.tcs = new TaskCompletionSource<bool>();

            var manifestUri = new Uri(url);
            mediaPlayer.Source = MediaSource.CreateFromUri(manifestUri);
            mediaPlayer.Play();

            await tcs.Task;
        }

        public void Stop()
        {
            mediaPlayer.Pause();
        }

        public void Dispose()
        {
            mediaPlayer.MediaEnded -= mediaEndHandler;
            mediaPlayer.BufferingStarted -= bufferingStartedHandler;
            mediaPlayer.Dispose();
        }

    }
}
