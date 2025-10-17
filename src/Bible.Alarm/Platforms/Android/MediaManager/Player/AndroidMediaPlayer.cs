using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Gms.Cast.Framework;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Support.V4.Media.Session;
using Com.Google.Android.Exoplayer2;
using Com.Google.Android.Exoplayer2.Ext.Cast;
using Com.Google.Android.Exoplayer2.Ext.Mediasession;
using Com.Google.Android.Exoplayer2.Source;
using Com.Google.Android.Exoplayer2.Source.Dash;
using Com.Google.Android.Exoplayer2.Source.Smoothstreaming;
using Com.Google.Android.Exoplayer2.Trackselection;
using Com.Google.Android.Exoplayer2.UI;
using Com.Google.Android.Exoplayer2.Upstream;
using Com.Google.Android.Exoplayer2.Util;
using MediaManager.Library;
using MediaManager.Media;
using MediaManager.Platforms.Android.Media;
using MediaManager.Platforms.Android.MediaSession;
using MediaManager.Platforms.Android.Queue;
using MediaManager.Platforms.Android.Video;
using MediaManager.Player;
using MediaManager.Video;

namespace MediaManager.Platforms.Android.Player
{
    public class AndroidMediaPlayer : MediaPlayerBase, IMediaPlayer<IPlayer, VideoView>
    {
        protected MediaManagerImplementation MediaManager => (MediaManagerImplementation)CrossMediaManager.Current;
        protected Dictionary<string, string> RequestHeaders => MediaManager.RequestHeaders;
        protected Context Context => MediaManager.Context;
        protected MediaSessionCompat MediaSession => MediaManager.MediaSession;

        protected string UserAgent { get; set; }
        protected DefaultHttpDataSourceFactory HttpDataSourceFactory { get; set; }
        public IDataSourceFactory DataSourceFactory { get; set; }
        public DefaultDashChunkSource.Factory DashChunkSourceFactory { get; set; }
        public DefaultSsChunkSource.Factory SsChunkSourceFactory { get; set; }

        protected DefaultBandwidthMeter BandwidthMeter { get; set; }
        protected AdaptiveTrackSelection.Factory TrackSelectionFactory { get; set; }
        protected DefaultTrackSelector TrackSelector { get; set; }

        public MediaSessionConnector MediaSessionConnector { get; set; }
        protected QueueNavigator QueueNavigator { get; set; }
        public ConcatenatingMediaSource MediaSource { get; set; }
        protected QueueDataAdapter QueueDataAdapter { get; set; }
        protected QueueMediaSourceFactory MediaSourceFactory { get; set; }
        protected TimelineQueueEditor TimelineQueueEditor { get; set; }
        protected MediaSessionConnectorPlaybackPreparer PlaybackPreparer { get; set; }
        public PlayerEventListener PlayerEventListener { get; set; }
        protected RatingCallback RatingCallback { get; set; }

        public IPlayer CurrentPlayer { get; set; }

        private Lazy<SimpleExoPlayer> exoplayer;
        public SimpleExoPlayer ExoPlayer
        {
            get
            {
                return exoplayer.Value;
            }
            set => throw new NotSupportedException();
        }

        public CastPlayer CastPlayer
        {
            get
            {
                return castPlayer.Value;
            }
            set => throw new NotSupportedException();
        }

        private Lazy<CastPlayer> castPlayer;

        public VideoView PlayerView => VideoView as VideoView;

        private IVideoView _videoView;
        public override IVideoView VideoView
        {
            get => _videoView;
            set
            {
                SetProperty(ref _videoView, value);
                if (PlayerView != null)
                {
                    PlayerView.RequestFocus();

                    //Use private field to prevent calling Initialize here
                    if (exoplayer != null)
                        PlayerView.Player = CurrentPlayer;

                    UpdateVideoView();
                }
            }
        }

        public IPlayer Player { get => CurrentPlayer; set => throw new NotImplementedException(); }

        public override void UpdateVideoAspect(VideoAspectMode videoAspectMode)
        {
            if (PlayerView == null)
                return;

            switch (videoAspectMode)
            {
                case VideoAspectMode.None:
                    PlayerView.ResizeMode = AspectRatioFrameLayout.ResizeModeZoom;
                    break;
                case VideoAspectMode.AspectFit:
                    PlayerView.ResizeMode = AspectRatioFrameLayout.ResizeModeFit;
                    break;
                case VideoAspectMode.AspectFill:
                    PlayerView.ResizeMode = AspectRatioFrameLayout.ResizeModeFill;
                    break;
                default:
                    PlayerView.ResizeMode = AspectRatioFrameLayout.ResizeModeZoom;
                    break;
            }
        }

        public override void UpdateShowPlaybackControls(bool showPlaybackControls)
        {
            if (PlayerView == null)
                return;

            PlayerView.UseController = showPlaybackControls;
        }

        public override void UpdateVideoPlaceholder(object value)
        {
            if (PlayerView == null)
                return;

            if (value is Drawable drawable)
            {
                PlayerView.UseArtwork = true;
                PlayerView.DefaultArtwork = drawable;
            }
            else if (value is Bitmap bmp)
            {
                PlayerView.UseArtwork = true;
                PlayerView.DefaultArtwork = new BitmapDrawable(Context.Resources, bmp);
            }
            else
                PlayerView.UseArtwork = false;
        }

        protected int lastWindowIndex = -1;

        public override event BeforePlayingEventHandler BeforePlaying;
        public override event AfterPlayingEventHandler AfterPlaying;

        public virtual void Initialize()
        {
            if (RequestHeaders?.Count > 0 && RequestHeaders.TryGetValue("User-Agent", out var userAgent))
                UserAgent = userAgent;
            else
                UserAgent = Util.GetUserAgent(Context, Context.PackageName);

            HttpDataSourceFactory = new DefaultHttpDataSourceFactory(UserAgent);
            UpdateRequestHeaders();

            MediaSource = new ConcatenatingMediaSource();

            DataSourceFactory = new DefaultDataSourceFactory(Context, HttpDataSourceFactory);
            DashChunkSourceFactory = new DefaultDashChunkSource.Factory(DataSourceFactory);
            SsChunkSourceFactory = new DefaultSsChunkSource.Factory(DataSourceFactory);

            ConnectMediaSession();


            PlayerEventListener = new PlayerEventListener()
            {
                OnPlayerErrorImpl = (ExoPlaybackException exception) =>
                {
                    switch (exception.Type)
                    {
                        case ExoPlaybackException.TypeRenderer:
                        case ExoPlaybackException.TypeSource:
                        case ExoPlaybackException.TypeUnexpected:
                            break;
                    }
                    MediaManager.OnMediaItemFailed(this, new MediaItemFailedEventArgs(MediaManager.Queue.Current, exception, exception.Message));
                },
                OnTracksChangedImpl = (trackGroups, trackSelections) =>
                {
                    BeforePlaying?.Invoke(this, new MediaPlayerEventArgs(MediaManager.Queue.Current, this));

                    MediaManager.Queue.CurrentIndex = Player.CurrentWindowIndex;

                    AfterPlaying?.Invoke(this, new MediaPlayerEventArgs(MediaManager.Queue.Current, this));
                },
                OnPlayerStateChangedImpl = (bool playWhenReady, int playbackState) =>
                {
                    switch (playbackState)
                    {
                        case IPlayer.StateEnded:
                            if (!Player.HasNext)
                                MediaManager.OnMediaItemFinished(this, new MediaItemEventArgs(MediaManager.Queue.Current));
                            //TODO: This means the whole list is finished. Should we fire an event?
                            break;
                        case IPlayer.StateIdle:
                            lastWindowIndex = -1;
                            break;
                        case IPlayer.StateBuffering:
                            //MediaManager.Buffered = TimeSpan.FromMilliseconds(Player.BufferedPosition);
                            break;
                        case IPlayer.StateReady:
                        default:
                            break;
                    }
                },
                OnPositionDiscontinuityImpl = (int reason) =>
                {
                    switch (reason)
                    {
                        case IPlayer.DiscontinuityReasonAdInsertion:
                        case IPlayer.DiscontinuityReasonSeek:
                        case IPlayer.DiscontinuityReasonSeekAdjustment:
                            break;
                        case IPlayer.DiscontinuityReasonPeriodTransition:
                            var currentWindowIndex = Player.CurrentWindowIndex;
                            if (SetProperty(ref lastWindowIndex, currentWindowIndex))
                            {
                                MediaManager.OnMediaItemFinished(this, new MediaItemEventArgs(MediaManager.Queue.Current));
                            }
                            break;
                        case IPlayer.DiscontinuityReasonInternal:
                            break;
                    }
                },
                OnLoadingChangedImpl = (bool isLoading) =>
                {
                    if (isLoading && Player.BufferedPosition >= 0)
                        MediaManager.Buffered = TimeSpan.FromMilliseconds(Player.BufferedPosition);
                },
                OnIsPlayingChangedImpl = (bool isPlaying) =>
                {
                    //TODO: Maybe call playing changed event
                },
                OnPlaybackSuppressionReasonChangedImpl = (int playbackSuppressionReason) =>
                {
                    //TODO: Maybe call event
                }
            };


            castPlayer = new Lazy<CastPlayer>(() =>
            {
                try
                {
                    var castContext = CastContext.GetSharedInstance(Application.Context);
                    var player = new CastPlayer(castContext);
                    player.AddListener(PlayerEventListener);
                    return player;
                }
                catch
                {
                    return null;
                }
            });

            exoplayer = new Lazy<SimpleExoPlayer>(() =>
            {
                var player = new SimpleExoPlayer.Builder(Context).Build();
                player.VideoSizeChanged += Player_VideoSizeChanged;

                var audioAttributes = new Com.Google.Android.Exoplayer2.Audio.AudioAttributes.Builder()
                 .SetUsage(C.UsageMedia)
                 .SetContentType(C.ContentTypeMusic)
                 .Build();

                player.SetAudioAttributes(audioAttributes, true);
                player.SetHandleAudioBecomingNoisy(true);
                player.SetWakeMode(C.WakeModeNetwork);

                player.AddListener(PlayerEventListener);

                if (PlayerView != null && PlayerView.Player == null)
                {
                    PlayerView.Player = player;
                }

                return player;
            });

        }

        private void Player_VideoSizeChanged(object sender, Com.Google.Android.Exoplayer2.Video.VideoSizeChangedEventArgs e)
        {
            VideoWidth = e.Width;
            VideoHeight = e.Height;
        }

        public virtual void UpdateRequestHeaders()
        {
            if (RequestHeaders?.Count > 0)
            {
                foreach (var item in RequestHeaders)
                {
                    HttpDataSourceFactory?.DefaultRequestProperties.Set(item.Key, item.Value);
                }
            }
        }

        public virtual void ConnectMediaSession()
        {
            if (MediaSession == null)
                throw new ArgumentNullException(nameof(MediaSession), $"{nameof(MediaSession)} cannot be null. Make sure the {nameof(MediaBrowserService)} sets it up");

            MediaSessionConnector = new MediaSessionConnector(MediaSession);
            MediaSessionConnector.SetMediaMetadataProvider(new MetaDataProvider(MediaSession.Controller, null));
            QueueNavigator = new QueueNavigator(MediaSession);
            MediaSessionConnector.SetQueueNavigator(QueueNavigator);

            QueueDataAdapter = new QueueDataAdapter(MediaSource);
            MediaSourceFactory = new QueueMediaSourceFactory();
            TimelineQueueEditor = new TimelineQueueEditor(MediaSession.Controller, MediaSource, QueueDataAdapter, MediaSourceFactory);
            MediaSessionConnector.SetQueueEditor(TimelineQueueEditor);

            RatingCallback = new RatingCallback();
            MediaSessionConnector.SetRatingCallback(RatingCallback);

            PlaybackPreparer = new MediaSessionConnectorPlaybackPreparer(MediaSource);
            MediaSessionConnector.SetPlaybackPreparer(PlaybackPreparer);
        }

        public override async Task Play(IMediaItem mediaItem)
        {
            BeforePlaying?.Invoke(this, new MediaPlayerEventArgs(mediaItem, this));
            await Play(mediaItem.ToMediaSource());
            AfterPlaying?.Invoke(this, new MediaPlayerEventArgs(mediaItem, this));
        }

        public override async Task Play(IMediaItem mediaItem, TimeSpan startAt, TimeSpan? stopAt = null)
        {
            BeforePlaying?.Invoke(this, new MediaPlayerEventArgs(mediaItem, this));

            var mediaSource = stopAt.HasValue ? mediaItem.ToClippingMediaSource(stopAt.Value) : mediaItem.ToMediaSource();
            MediaSource.Clear();
            MediaSource.AddMediaSource(mediaSource);

            if (startAt != TimeSpan.Zero)
                await SeekTo(startAt);

            await Play();

            AfterPlaying?.Invoke(this, new MediaPlayerEventArgs(mediaItem, this));
        }

        public virtual async Task Play(IMediaSource mediaSource)
        {
            MediaSource.Clear();
            MediaSource.AddMediaSource(mediaSource);
            await Play();
        }

        public override Task Play()
        {
            Player.PlayWhenReady = true;
            return Task.CompletedTask;
        }

        public override Task Pause()
        {
            Player.Stop();
            return Task.CompletedTask;
        }

        public override Task SeekTo(TimeSpan position)
        {
            Player.SeekTo((long)position.TotalMilliseconds);
            return Task.CompletedTask;
        }

        public override Task Stop()
        {
            Player.Stop(true);
            return Task.CompletedTask;
        }

        private bool disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (exoplayer.IsValueCreated)
            {
                ExoPlayer.VideoSizeChanged -= Player_VideoSizeChanged;
                ExoPlayer.RemoveListener(PlayerEventListener);
                ExoPlayer.Release();
                ExoPlayer.Dispose();
            }

            if (castPlayer.IsValueCreated)
            {
                CastPlayer.RemoveListener(PlayerEventListener);
                CastPlayer.Release();
                CastPlayer.Dispose();
            }

            disposed = true;
        }
    }
}
