using System;
using System.Linq;
using System.Threading.Tasks;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.Media.Session;
using Bible.Alarm;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.Services.Droid.Helpers;
using Com.Google.Android.Exoplayer2;
using Com.Google.Android.Exoplayer2.Ext.Cast;
using Com.Google.Android.Exoplayer2.Ext.Mediasession;
using Com.Google.Android.Exoplayer2.Source;
using MediaManager.Platforms.Android.Media;
using NLog;

namespace MediaManager.Platforms.Android.Player
{
    public class MediaSessionConnectorPlaybackPreparer : Java.Lang.Object,
        MediaSessionConnector.IPlaybackPreparer
    {
        private static readonly Lazy<Logger> LazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger Logger => LazyLogger.Value;

        private Bible.Alarm.IContainer _container;

        protected MediaManagerImplementation MediaManager => (MediaManagerImplementation)CrossMediaManager.Current;
        protected IPlayer CurrentPlayer => MediaManager.AndroidMediaPlayer.CurrentPlayer;

        ConcatenatingMediaSource _mediaSource;
        private IPlaybackService _playbackService;
        public MediaSessionConnectorPlaybackPreparer(ConcatenatingMediaSource mediaSource)
        {
            this._mediaSource = mediaSource;
            _container = BootstrapHelper.GetInitializedContainer();
            _playbackService = _container.Resolve<IPlaybackService>();
        }

        protected MediaSessionConnectorPlaybackPreparer(IntPtr handle, JniHandleOwnership transfer)
            : base(handle, transfer)
        {
        }

        public long SupportedPrepareActions =>
            PlaybackStateCompat.ActionPrepare |
            PlaybackStateCompat.ActionPrepareFromMediaId |
            PlaybackStateCompat.ActionPrepareFromSearch;

        public bool OnCommand(IPlayer player, IControlDispatcher controlDispatcher, string command, Bundle extras, ResultReceiver cb)
        {
            return false;
        }

        public void OnPrepare(bool playWhenReady)
        {
            if (_mediaSource.Size > 0)
            {
                Prepare(playWhenReady);
                return;
            }

            Task.Run(async () => await _playbackService.PrepareRelavantPlaylist()).Wait();
            Prepare(playWhenReady);
        }

        public void OnPrepareFromMediaId(string mediaId, bool playWhenReady, Bundle extras)
        {
            Task.Run(async () => await _playbackService.PrepareRelavantPlaylist()).Wait();
            Prepare(playWhenReady);
        }

        public void OnPrepareFromSearch(string query, bool playWhenReady, Bundle extras)
        {
            Task.Run(async () => await _playbackService.PrepareRelavantPlaylist()).Wait();
            Prepare(playWhenReady);
        }

        public void OnPrepareFromUri(global::Android.Net.Uri uri, bool playWhenReady, Bundle extras)
        {
            return;
        }

        private void Prepare(bool playWhenReady)
        {
            Prepare(playWhenReady, CurrentPlayer, MediaManager, _playbackService, _mediaSource);
        }

        public static void Prepare(bool playWhenReady, IPlayer currentPlayer,
                IMediaManager mediaManager, IPlaybackService playbackService,
                ConcatenatingMediaSource mediaSource)
        {
            try
            {
                currentPlayer.PlayWhenReady = playWhenReady || mediaManager.AutoPlay;
                currentPlayer.Stop(true);

                var currentTrackIndex = playbackService.CurrentTrackIndex;
                var currentTrackPosition = playbackService.CurrentTrackPosition;
                var seek = currentTrackIndex >= 0 && currentTrackPosition != default;
                if (currentPlayer is SimpleExoPlayer)
                {
                    (currentPlayer as SimpleExoPlayer).Prepare(mediaSource);
                    if (seek)
                    {
                        currentPlayer.SeekTo(currentTrackIndex, (long)currentTrackPosition.TotalMilliseconds);
                    }
                }
                else
                {
                    var castPlayer = currentPlayer as CastPlayer;
                    castPlayer.LoadItems(mediaManager.Queue.Select(x => x.ToMediaQueueItem()).ToArray(),
                        seek ? currentTrackIndex : 0,
                        seek ? (long)currentTrackPosition.TotalMilliseconds : 0, IPlayer.RepeatModeOff);
                }

            }
            catch (Exception e)
            {
                Logger.Error(e, "An error happened when preparing.");
            }
        }

    }
}
