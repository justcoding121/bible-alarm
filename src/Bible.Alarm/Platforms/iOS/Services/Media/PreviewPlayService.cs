using AVFoundation;
using Bible.Alarm.Services.Contracts;
using Foundation;
using System;
using System.IO;
using System.Threading.Tasks;


namespace Bible.Alarm.Services.iOS
{
    public class PreviewPlayService : IPreviewPlayService, IDisposable
    {
        private readonly IContainer container;
        private AVAudioPlayer player;
        private IDownloadService downloadService;

        public event Action OnStopped;

        public PreviewPlayService(IContainer container, IDownloadService downloadService)
        {
            this.container = container;
            this.downloadService = downloadService;
        }

        ///<Summary>
        /// Load wave or mp3 audio file from the Android assets folder
        ///</Summary>
        private async Task<bool> load(string url)
        {
            deletePlayer();

            var bytes = await downloadService.DownloadAsync(url);
            using var stream = new MemoryStream(bytes);
            var data = NSData.FromStream(stream);
            player = AVAudioPlayer.FromData(data);

            return preparePlayer();
        }

        private bool preparePlayer()
        {
            if (player != null)
            {
                player.FinishedPlaying += OnPlaybackEnded;
                player.PrepareToPlay();
            }

            return (player == null) ? false : true;
        }


        public async Task Play(string url)
        {
            if (await load(url))
            {
                if (player == null)
                    return;

                if (player.Playing)
                    player.CurrentTime = 0;
                else
                    player?.Play();
            }
        }

        public void Stop()
        {
            player?.Stop();
        }

        private void deletePlayer()
        {
            Stop();

            if (player != null)
            {
                player.FinishedPlaying -= OnPlaybackEnded;
                player.Dispose();
                player = null;
            }
        }

        private void OnPlaybackEnded(object sender, AVStatusEventArgs e)
        {
            OnStopped?.Invoke();
        }

        public void Dispose()
        {
            deletePlayer();
        }
    }
}
