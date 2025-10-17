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
        private MediaPlayer player = player;

        public event Action OnStopped;

        public void Stop()
        {
            player.Stop();
        }

        public void OnCompletion(MediaPlayer mp)
        {
            OnStopped?.Invoke();
        }

        Task IPreviewPlayService.Play(string url)
        {
            var uri = Android.Net.Uri.Parse(url);
            this.player.Reset();
            this.player.SetOnCompletionListener(this);
            this.player.SetDataSource(container.AndroidContext(), uri);
            this.player.Prepare();
            this.player.Start();

            return Task.CompletedTask;
        }

        private bool disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            this.player?.Stop();
            this.player?.Dispose();
            this.player = null;

            disposed = true;
            base.Dispose(disposing);
        }
    }
}
