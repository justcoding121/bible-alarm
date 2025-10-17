using System;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using AndroidX.Core.Content;
using AndroidX.Media;
using Bible.Alarm;
using Bible.Alarm.Droid;
using Bible.Alarm.Droid.Services.Platform;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.Services.Droid.Helpers;
using Bible.Alarm.Services.Infrastructure;
using Com.Google.Android.Exoplayer2;
using Com.Google.Android.Exoplayer2.Ext.Mediasession;
using Com.Google.Android.Exoplayer2.UI;
using Com.Google.Android.Exoplayer2.Ext.Cast;
using MediaManager.Platforms.Android.Media;
using NLog;
using Android.Gms.Cast.Framework;
using MediaManager.Platforms.Android.Player;
using MediaManager.Library;
using System.IO;
using AndroidX.Media.Session;
using System.Threading;
using Newtonsoft.Json;

namespace MediaManager.Platforms.Android.MediaSession
{
    [Service(Exported = true, Enabled = true)]
    [IntentFilter(new[] { global::Android.Service.Media.MediaBrowserService.ServiceInterface })]
    public class MediaBrowserService : MediaBrowserServiceCompat
    {
        private static readonly Lazy<Logger> lazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger logger => lazyLogger.Value;

        protected MediaManagerImplementation MediaManager => (MediaManagerImplementation)CrossMediaManager.Current;
        protected MediaDescriptionAdapter MediaDescriptionAdapter { get; set; }
        protected PlayerNotificationManager PlayerNotificationManager
        {
            get => (MediaManager.Notification as Notifications.NotificationManager).PlayerNotificationManager;
            set => (MediaManager.Notification as Notifications.NotificationManager).PlayerNotificationManager = value;
        }
        protected MediaControllerCompat MediaController => MediaManager.MediaController;

        protected NotificationListener NotificationListener { get; set; }

        public readonly string ChannelId = "com.jthomas.info.Bible.Alarm.NOW_PLAYING";
        public readonly int NotificationId = 1;

        public bool IsForegroundService = false;

        private IContainer container;

        public MediaBrowserService()
        {
            LogSetup.Initialize(VersionFinder.Default,
                new string[] { $"AndroidSdk {Build.VERSION.SdkInt}" }, Xamarin.Forms.Device.Android);

            AppDomain.CurrentDomain.UnhandledException += unhandledExceptionHandler;
            TaskScheduler.UnobservedTaskException += unobserverdTaskException;
        }

        private void unobserverdTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            logger.Error(e.Exception, "Unobserved task exception.");
        }

        private void unhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            logger.Error("Unhandled exception.", e.SerializeObject());
        }

        protected MediaBrowserService(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        {

        }

        private IMediaItem relavantMedia;

        public IPlayer CurrentPlayer { get => MediaManager.AndroidMediaPlayer.CurrentPlayer; set { MediaManager.AndroidMediaPlayer.CurrentPlayer = value; } }
        public SimpleExoPlayer ExoPlayer => MediaManager.AndroidMediaPlayer.ExoPlayer;
        public CastPlayer CastPlayer => MediaManager.AndroidMediaPlayer.CastPlayer;


        private PlayerEventListener playerListener;

        public override void OnCreate()
        {
            base.OnCreate();

            try
            {
                container = BootstrapHelper.InitializeService(this);
            }
            catch (Exception e)
            {
                logger.Error(e, "An error happened when calling BootstrapHelper from MediaBrowserService.");
            }

            //create media session and connect
            var mediaManager = container.Resolve<IMediaManager>() as MediaManagerImplementation;

            //async prepare call here
            var prepareMediaTask = Task.Run(async () => await prepareMedia());

            try
            {
                playerListener = new PlayerEventListener();
                playerListener.OnPlayerErrorImpl += onPlayerError;
                playerListener.OnPlayerStateChangedImpl += onPlayerStateChanged;

                NotificationListener = new NotificationListener();

                var mediaSession = MediaManager.MediaSession = new MediaSessionCompat(this, nameof(MediaBrowserService), null, MediaManager.SessionActivityPendingIntent);

                mediaSession.Active = true;

                SessionToken = mediaSession.SessionToken;

                MediaDescriptionAdapter = new MediaDescriptionAdapter(new MediaControllerCompat(this, SessionToken));
                PlayerNotificationManager = PlayerNotificationManager.CreateWithNotificationChannel(
                    this,
                    ChannelId,
                    Resource.String.notification_channel,
                    Resource.String.notification_channel_description,
                    NotificationId,
                    MediaDescriptionAdapter,
                    NotificationListener);

                PlayerNotificationManager.SetMediaSessionToken(SessionToken);
                PlayerNotificationManager.SetSmallIcon(MediaManager.NotificationIconResource);

                PlayerNotificationManager.SetRewindIncrementMs((long)MediaManager.StepSizeBackward.TotalMilliseconds);
                PlayerNotificationManager.SetFastForwardIncrementMs((long)MediaManager.StepSizeForward.TotalMilliseconds);

                PlayerNotificationManager.SetUsePlayPauseActions(MediaManager.Notification.ShowPlayPauseControls);
                PlayerNotificationManager.SetUseNavigationActions(MediaManager.Notification.ShowNavigationControls);

                mediaManager.Init(Application.Context);
                mediaManager.AndroidMediaPlayer.Initialize();

                if (CastPlayer != null)
                {
                    CastPlayer.SetSessionAvailabilityListener(new CastSessionAvailabilityListener(this));
                }

                mediaSessionConnector = mediaManager.AndroidMediaPlayer.MediaSessionConnector;

                SwitchToPlayer(null, CastPlayer != null && CastPlayer.IsCastSessionAvailable ? (IPlayer)CastPlayer : ExoPlayer);

                PlayerNotificationManager.NotificationPosted += onNotificationPosted;
                PlayerNotificationManager.NotificationCancelled += onNotificationCancelled;

                PlayerNotificationManager.SetPlayer(CurrentPlayer);

                relavantMedia = prepareMediaTask.Result;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error happened when initializing MediaBrowserService");
            }
        }

        private class CastSessionAvailabilityListener : Java.Lang.Object, ISessionAvailabilityListener
        {
            private MediaBrowserService service;
            public CastSessionAvailabilityListener(MediaBrowserService service)
            {
                this.service = service;
            }

            public void OnCastSessionUnavailable()
            {
                service.SwitchToPlayer(service.CurrentPlayer, service.CastPlayer);
            }

            public void OnCastSessionAvailable()
            {
                service.SwitchToPlayer(service.CurrentPlayer, service.ExoPlayer);
            }
        }

        public void SwitchToPlayer(IPlayer previousPlayer, IPlayer newPlayer)
        {
            if (previousPlayer == newPlayer)
            {
                return;
            }

            CurrentPlayer = newPlayer;
            if (previousPlayer != null)
            {
                previousPlayer.RemoveListener(playerListener);

                var playbackState = previousPlayer.PlaybackState;
                if (MediaManager.Queue.Count == 0)
                {
                    CurrentPlayer.Stop(true);
                }
                else if (playbackState != IPlayer.StateIdle && playbackState != IPlayer.StateEnded)
                {
                    var playbackService = container.Resolve<IPlaybackService>();
                    //prepare playlist
                    Task.Run(async () => await playbackService.PrepareRelavantPlaylist()).Wait();
                    MediaSessionConnectorPlaybackPreparer.Prepare(true, CurrentPlayer, MediaManager, playbackService,
                         MediaManager.AndroidMediaPlayer.MediaSource);
                }
            }

            newPlayer.AddListener(playerListener);
            mediaSessionConnector.SetPlayer(newPlayer);
            previousPlayer?.Stop(true);
        }

        private void onPlayerError(ExoPlaybackException e)
        {
            switch (e.Type)
            {
                case ExoPlaybackException.TypeSource:
                    logger.Error(e, e.SourceException.Message);
                    break;
                case ExoPlaybackException.TypeRenderer:
                    logger.Error(e, e.RendererException.Message);
                    break;
                case ExoPlaybackException.TypeUnexpected:
                    logger.Error(e, e.UnexpectedException.Message);
                    break;
                case ExoPlaybackException.TypeOutOfMemory:
                    logger.Error(e, e.OutOfMemoryError.Message);
                    break;
                case ExoPlaybackException.TypeRemote:
                    logger.Error(e, e.Message);
                    break;
            }

        }

        private void onPlayerStateChanged(bool playWhenReady, int playbackState)
        {
            switch (playbackState)
            {
                case IPlayer.StateBuffering:
                case IPlayer.StateReady:
                    PlayerNotificationManager.SetPlayer(CurrentPlayer);
                    if (playbackState == IPlayer.StateReady)
                    {
                        if (!playWhenReady)
                        {
                            StopForeground(false);
                            IsForegroundService = false;
                        }
                    }
                    break;
                default:
                    PlayerNotificationManager.SetPlayer(null);
                    break;
            }
        }

        private const string channelName = "Now Playing";
        private const string channelDescription = "New World Translation";

        private MediaSessionConnector mediaSessionConnector;

        private void onNotificationCancelled(object sender, PlayerNotificationManager.NotificationCancelledEventArgs e)
        {
            var dismissedByUser = e.DismissedByUser;

            StopForeground(true);
            IsForegroundService = false;

            StopSelf();
        }

        private void onNotificationPosted(object sender, PlayerNotificationManager.NotificationPostedEventArgs e)
        {
            var isOnGoing = e.Ongoing;
            var notificationId = e.NotificationId;
            var notification = e.Notification;

            //playing state
            if (isOnGoing && !IsForegroundService)
            {
                ContextCompat.StartForegroundService(ApplicationContext, new Intent(ApplicationContext, Java.Lang.Class.FromType(typeof(MediaBrowserService))));
                StartForeground(notificationId, notification);
                IsForegroundService = true;
            }
        }

        public override StartCommandResult OnStartCommand(Intent startIntent, StartCommandFlags flags, int startId)
        {
            if (startIntent != null)
            {
                MediaButtonReceiver.HandleIntent(MediaManager.MediaSession, startIntent);
            }

            return StartCommandResult.Sticky;
        }

        public override void OnTaskRemoved(Intent rootIntent)
        {
            base.OnTaskRemoved(rootIntent);

            CurrentPlayer.Stop(true);
        }

        private const string MEDIA_SEARCH_SUPPORTED = "android.media.browse.SEARCH_SUPPORTED";
        private const string CONTENT_STYLE_SUPPORTED = "android.media.browse.CONTENT_STYLE_SUPPORTED";

        private const string UAMP_BROWSABLE_ROOT = "/";
        private const string UAMP_RECENT_ROOT = "__RECENT__";

        public override BrowserRoot OnGetRoot(string clientPackageName, int clientUid, Bundle rootHints)
        {
            var rootExtras = new Bundle();

            rootExtras.PutBoolean(MEDIA_SEARCH_SUPPORTED, false);
            rootExtras.PutBoolean(CONTENT_STYLE_SUPPORTED, false);

            var isRecentRequest = rootHints != null && rootHints.GetBoolean(BrowserRoot.ExtraRecent);
            var browserRootPath = isRecentRequest ? UAMP_RECENT_ROOT : UAMP_BROWSABLE_ROOT;

            return new BrowserRoot(browserRootPath, rootExtras);
        }

        public override void OnLoadChildren(string parentId, Result result)
        {
            try
            {
                if (MediaManager.Queue.Count > 0)
                {
                    setResult();
                    return;
                }

                if (relavantMedia != null)
                {
                    result.SendResult(new JavaList<MediaBrowserCompat.MediaItem>() { relavantMedia.ToMediaBrowserMediaItem() });
                    return;
                }

                result.SendResult(new JavaList<MediaBrowserCompat.MediaItem>());
                return;
            }
            catch (Exception e)
            {
                logger.Error(e, "An error happened when loading children.");
            }

            void setResult()
            {
                var mediaItems = new JavaList<MediaBrowserCompat.MediaItem>();

                foreach (var item in MediaManager.Queue)
                    mediaItems.Add(item.ToMediaBrowserMediaItem());

                result.SendResult(mediaItems);
            }

            result.SendResult(new JavaList<MediaBrowserCompat.MediaItem>());
        }

        private async Task<IMediaItem> prepareMedia()
        {
            try
            {
                if (MediaManager.Queue.Count > 0)
                {
                    return MediaManager.Queue[0];
                }

                using var playlistService = container.Resolve<IPlaylistService>();

                var lastPlayed = await playlistService.GetRelavantScheduleToPlay();
                var nextTrack = await playlistService.NextTrack(lastPlayed);

                using var cacheService = container.Resolve<IMediaCacheService>();

                var mediaExtractor = MediaManager.Extractor;

                if (await cacheService.Exists(nextTrack.Url))
                {
                    return await mediaExtractor.CreateMediaItem(new FileInfo(cacheService.GetCacheFilePath(nextTrack.Url)));
                }

                return await mediaExtractor.CreateMediaItem(nextTrack.Url);

            }
            catch (Exception e)
            {
                logger.Error(e, "An error happened when calling AlarmHandler from PlaybackPreparer.");
            }

            return null;
        }


        public override void OnDestroy()
        {
            try
            {
                playerListener.OnPlayerErrorImpl -= onPlayerError;
                playerListener.OnPlayerStateChangedImpl -= onPlayerStateChanged;

                PlayerNotificationManager.NotificationPosted += onNotificationPosted;
                PlayerNotificationManager.NotificationCancelled += onNotificationCancelled;

                MediaManager.MediaSession.Active = false;
                MediaManager.MediaSession.Release();

                (MediaManager.Notification as Notifications.NotificationManager).Player = null;

                MediaDescriptionAdapter.Dispose();
                MediaDescriptionAdapter = null;

                PlayerNotificationManager.SetPlayer(null);
                PlayerNotificationManager.Dispose();
                PlayerNotificationManager = null;

                NotificationListener.Dispose();
                NotificationListener = null;

                MediaManager.MediaSession.Active = false;
                MediaManager.MediaSession.Release();
                MediaManager.MediaSession = null;

                if (CastPlayer != null)
                {
                    CastPlayer.SetSessionAvailabilityListener(new CastSessionAvailabilityListener(this));
                }

                CurrentPlayer.RemoveListener(playerListener);
            }
            catch (Exception e)
            {
                logger.Error(e, "An error happened when disposing MediaBrowserService");
            }

            base.OnDestroy();
        }


        private bool disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }


            BootstrapHelper.Remove(this);

            AppDomain.CurrentDomain.UnhandledException -= unhandledExceptionHandler;
            TaskScheduler.UnobservedTaskException -= unobserverdTaskException;

            disposed = true;
            base.Dispose(disposing);
        }

    }
}
