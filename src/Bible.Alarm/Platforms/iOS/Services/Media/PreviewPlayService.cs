using AVFoundation;
using Bible.Alarm.Services.Contracts;
using Foundation;
using System;
using System.IO;
using System.Threading.Tasks;


namespace Bible.Alarm.Services.iOS
{
    public class PreviewPlayService(IContainer container, IDownloadService downloadService)
        : IPreviewPlayService, IDisposable
    {
        private readonly IContainer _container = container;
        private AVAudioPlayer _player;

        public event Action OnStopped;

        ///<Summary>
        /// Load wave or mp3 audio file from the Android assets folder
        ///</Summary>
        private async Task<bool> Load(string url)
        {
            DeletePlayer();

            var bytes = await downloadService.DownloadAsync(url);
            using var stream = new MemoryStream(bytes);
            var data = NSData.FromStream(stream);
            _player = AVAudioPlayer.FromData(data);

            return PreparePlayer();
        }

        private bool PreparePlayer()
        {
            if (_player != null)
            {
                _player.FinishedPlaying += OnPlaybackEnded;
                _player.PrepareToPlay();
            }

            return (_player == null) ? false : true;
        }


        public async Task Play(string url)
        {
            if (await Load(url))
            {
                if (_player == null)
                    return;

                if (_player.Playing)
                    _player.CurrentTime = 0;
                else
                    _player?.Play();
            }
        }

        public void Stop()
        {
            _player?.Stop();
        }

        private void DeletePlayer()
        {
            Stop();

            if (_player != null)
            {
                _player.FinishedPlaying -= OnPlaybackEnded;
                _player.Dispose();
                _player = null;
            }
        }

        private void OnPlaybackEnded(object sender, AVStatusEventArgs e)
        {
            OnStopped?.Invoke();
        }

        public void Dispose()
        {
            DeletePlayer();
        }
    }
}
