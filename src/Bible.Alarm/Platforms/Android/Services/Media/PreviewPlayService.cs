using Android.Media;
using Bible.Alarm.Droid;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.Services.Droid.Extensions;
using System;
using System.Threading.Tasks;


namespace Bible.Alarm.Services.Droid
{
    public class PreviewPlayService(IContainer container, MediaPlayer player) : Java.Lang.Object,
        MediaPlayer.IOnCompletionListener, IPreviewPlayService, IDisposable
    {
        private MediaPlayer _player = player;

        public event Action OnStopped;

        public void Stop()
        {
            _player.Stop();
        }

        public void OnCompletion(MediaPlayer mp)
        {
            OnStopped?.Invoke();
        }

        Task IPreviewPlayService.Play(string url)
        {
            var uri = Android.Net.Uri.Parse(url);
            this._player.Reset();
            this._player.SetOnCompletionListener(this);
            this._player.SetDataSource(container.AndroidContext(), uri);
            this._player.Prepare();
            this._player.Start();

            return Task.CompletedTask;
        }

        private bool _disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            this._player?.Stop();
            this._player?.Dispose();
            this._player = null;

            _disposed = true;
            base.Dispose(disposing);
        }
    }
}
