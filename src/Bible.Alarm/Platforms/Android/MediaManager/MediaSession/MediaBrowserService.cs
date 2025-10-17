using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using AndroidX.Core.Content;
using AndroidX.Media;
using Bible.Alarm.Droid.Services.Platform;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.Services.Droid.Helpers;
using Bible.Alarm.Services.Infrastructure;
using Com.Google.Android.Exoplayer2;
using Com.Google.Android.Exoplayer2.Ext.Mediasession;
using Com.Google.Android.Exoplayer2.UI;
using Com.Google.Android.Exoplayer2.Ext.Cast;
using NLog;
using AndroidX.Media.Session;
using MediaManager.Library;
using MediaManager.Platforms.Android.Media;
using MediaManager.Platforms.Android.Player;

namespace MediaManager.Platforms.Android.MediaSession
{
    [Service(Exported = true, Enabled = true)]
    [IntentFilter(new[] { global::Android.Service.Media.MediaBrowserService.ServiceInterface })]
    public class MediaBrowserService : MediaBrowserServiceCompat
    {
        private static readonly Lazy<Logger> LazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger Logger => LazyLogger.Value;

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

        private Bible.Alarm.IContainer _container;

        public MediaBrowserService()
        {
            LogSetup.Initialize(VersionFinder.Default,
                [$"AndroidSdk {Build.VERSION.SdkInt}"], "Android");

            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
            TaskScheduler.UnobservedTaskException += UnobserverdTaskException;
        }

        private void UnobserverdTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Logger.Error(e.Exception, "Unobserved task exception.");
        }

        private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.Error("Unhandled exception.", e.SerializeObject());
        }

        protected MediaBrowserService(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        {

        }

        private IMediaItem _relavantMedia;

        public IPlayer CurrentPlayer { get => MediaManager.AndroidMediaPlayer.CurrentPlayer; set { MediaManager.AndroidMediaPlayer.CurrentPlayer = value; } }
        public SimpleExoPlayer ExoPlayer => MediaManager.AndroidMediaPlayer.ExoPlayer;
        public CastPlayer CastPlayer => MediaManager.AndroidMediaPlayer.CastPlayer;


        private PlayerEventListener _playerListener;

        public override void OnCreate()
        {
            base.OnCreate();

            try
            {
                _container = BootstrapHelper.InitializeService(this);
            }
            catch (Exception e)
            {
                Logger.Error(e, "An error happened when calling BootstrapHelper from MediaBrowserService.");
            }

            //create media session and connect
            var mediaManager = _container.Resolve<IMediaManager>() as MediaManagerImplementation;

            //async prepare call here
            var prepareMediaTask = Task.Run(async () => await PrepareMedia());

            try
            {
                _playerListener = new PlayerEventListener();
                _playerListener.OnPlayerErrorImpl += OnPlayerError;
                _playerListener.OnPlayerStateChangedImpl += OnPlayerStateChanged;

                NotificationListener = new NotificationListener();

                var mediaSession = MediaManager.MediaSession = new MediaSessionCompat(this, nameof(MediaBrowserService), null, MediaManager.SessionActivityPendingIntent);

                mediaSession.Active = true;

                SessionToken = mediaSession.SessionToken;

                MediaDescriptionAdapter = new MediaDescriptionAdapter(new MediaControllerCompat(this, SessionToken));
                PlayerNotificationManager = PlayerNotificationManager.CreateWithNotificationChannel(
                    this,
                    ChannelId,
                    Bible.Alarm.Resource.String.notification_channel,
                    Bible.Alarm.Resource.String.notification_channel_description,
                    NotificationId,
                    MediaDescriptionAdapter,
                    NotificationListener);

                PlayerNotificationManager.SetMediaSessionToken(SessionToken);
                PlayerNotificationManager.SetSmallIcon(MediaManager.NotificationIconResource);

                PlayerNotificationManager.SetRewindIncrementMs((long)MediaManager.StepSizeBackward.TotalMilliseconds);
                PlayerNotificationManager.SetFastForwardIncrementMs((long)MediaManager.StepSizeForward.TotalMilliseconds);

                PlayerNotificationManager.SetUsePlayPauseActions(MediaManager.Notification.ShowPlayPauseControls);
                PlayerNotificationManager.SetUseNavigationActions(MediaManager.Notification.ShowNavigationControls);

                mediaManager.Init(global::Android.App.Application.Context);
                mediaManager.AndroidMediaPlayer.Initialize();

                if (CastPlayer != null)
                {
                    CastPlayer.SetSessionAvailabilityListener(new CastSessionAvailabilityListener(this));
                }

                _mediaSessionConnector = mediaManager.AndroidMediaPlayer.MediaSessionConnector;

                SwitchToPlayer(null, CastPlayer != null && CastPlayer.IsCastSessionAvailable ? (IPlayer)CastPlayer : ExoPlayer);

                PlayerNotificationManager.NotificationPosted += OnNotificationPosted;
                PlayerNotificationManager.NotificationCancelled += OnNotificationCancelled;

                PlayerNotificationManager.SetPlayer(CurrentPlayer);

                _relavantMedia = prepareMediaTask.Result;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error happened when initializing MediaBrowserService");
            }
        }

        private class CastSessionAvailabilityListener(MediaBrowserService service)
            : Java.Lang.Object, ISessionAvailabilityListener
        {
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
                previousPlayer.RemoveListener(_playerListener);

                var playbackState = previousPlayer.PlaybackState;
                if (MediaManager.Queue.Count == 0)
                {
                    CurrentPlayer.Stop(true);
                }
                else if (playbackState != IPlayer.StateIdle && playbackState != IPlayer.StateEnded)
                {
                    var playbackService = _container.Resolve<IPlaybackService>();
                    //prepare playlist
                    Task.Run(async () => await playbackService.PrepareRelavantPlaylist()).Wait();
                    MediaSessionConnectorPlaybackPreparer.Prepare(true, CurrentPlayer, MediaManager, playbackService,
                         MediaManager.AndroidMediaPlayer.MediaSource);
                }
            }

            newPlayer.AddListener(_playerListener);
            _mediaSessionConnector.SetPlayer(newPlayer);
            previousPlayer?.Stop(true);
        }

        private void OnPlayerError(ExoPlaybackException e)
        {
            switch (e.Type)
            {
                case ExoPlaybackException.TypeSource:
                    Logger.Error(e, e.SourceException.Message);
                    break;
                case ExoPlaybackException.TypeRenderer:
                    Logger.Error(e, e.RendererException.Message);
                    break;
                case ExoPlaybackException.TypeUnexpected:
                    Logger.Error(e, e.UnexpectedException.Message);
                    break;
                case ExoPlaybackException.TypeOutOfMemory:
                    Logger.Error(e, e.OutOfMemoryError.Message);
                    break;
                case ExoPlaybackException.TypeRemote:
                    Logger.Error(e, e.Message);
                    break;
            }

        }

        private void OnPlayerStateChanged(bool playWhenReady, int playbackState)
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

        private const string ChannelName = "Now Playing";
        private const string ChannelDescription = "New World Translation";

        private MediaSessionConnector _mediaSessionConnector;

        private void OnNotificationCancelled(object sender, PlayerNotificationManager.NotificationCancelledEventArgs e)
        {
            var dismissedByUser = e.DismissedByUser;

            StopForeground(true);
            IsForegroundService = false;

            StopSelf();
        }

        private void OnNotificationPosted(object sender, PlayerNotificationManager.NotificationPostedEventArgs e)
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

        private const string MediaSearchSupported = "android.media.browse.SEARCH_SUPPORTED";
        private const string ContentStyleSupported = "android.media.browse.CONTENT_STYLE_SUPPORTED";

        private const string UampBrowsableRoot = "/";
        private const string UampRecentRoot = "__RECENT__";

        public override BrowserRoot OnGetRoot(string clientPackageName, int clientUid, Bundle rootHints)
        {
            var rootExtras = new Bundle();

            rootExtras.PutBoolean(MediaSearchSupported, false);
            rootExtras.PutBoolean(ContentStyleSupported, false);

            var isRecentRequest = rootHints != null && rootHints.GetBoolean(BrowserRoot.ExtraRecent);
            var browserRootPath = isRecentRequest ? UampRecentRoot : UampBrowsableRoot;

            return new BrowserRoot(browserRootPath, rootExtras);
        }

        public override void OnLoadChildren(string parentId, Result result)
        {
            try
            {
                if (MediaManager.Queue.Count > 0)
                {
                    SetResult();
                    return;
                }

                if (_relavantMedia != null)
                {
                    result.SendResult(new JavaList<MediaBrowserCompat.MediaItem>() { _relavantMedia.ToMediaBrowserMediaItem() });
                    return;
                }

                result.SendResult(new JavaList<MediaBrowserCompat.MediaItem>());
                return;
            }
            catch (Exception e)
            {
                Logger.Error(e, "An error happened when loading children.");
            }

            void SetResult()
            {
                var mediaItems = new JavaList<MediaBrowserCompat.MediaItem>();

                foreach (var item in MediaManager.Queue)
                    mediaItems.Add(item.ToMediaBrowserMediaItem());

                result.SendResult(mediaItems);
            }

            result.SendResult(new JavaList<MediaBrowserCompat.MediaItem>());
        }

        private async Task<IMediaItem> PrepareMedia()
        {
            try
            {
                if (MediaManager.Queue.Count > 0)
                {
                    return MediaManager.Queue[0];
                }

                using var playlistService = _container.Resolve<IPlaylistService>();

                var lastPlayed = await playlistService.GetRelavantScheduleToPlay();
                var nextTrack = await playlistService.NextTrack(lastPlayed);

                using var cacheService = _container.Resolve<IMediaCacheService>();

                var mediaExtractor = MediaManager.Extractor;

                if (await cacheService.Exists(nextTrack.Url))
                {
                    return await mediaExtractor.CreateMediaItem(new FileInfo(cacheService.GetCacheFilePath(nextTrack.Url)));
                }

                return await mediaExtractor.CreateMediaItem(nextTrack.Url);

            }
            catch (Exception e)
            {
                Logger.Error(e, "An error happened when calling AlarmHandler from PlaybackPreparer.");
            }

            return null;
        }


        public override void OnDestroy()
        {
            try
            {
                _playerListener.OnPlayerErrorImpl -= OnPlayerError;
                _playerListener.OnPlayerStateChangedImpl -= OnPlayerStateChanged;

                PlayerNotificationManager.NotificationPosted += OnNotificationPosted;
                PlayerNotificationManager.NotificationCancelled += OnNotificationCancelled;

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

                CurrentPlayer.RemoveListener(_playerListener);
            }
            catch (Exception e)
            {
                Logger.Error(e, "An error happened when disposing MediaBrowserService");
            }

            base.OnDestroy();
        }


        private bool _disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }


            BootstrapHelper.Remove(this);

            AppDomain.CurrentDomain.UnhandledException -= UnhandledExceptionHandler;
            TaskScheduler.UnobservedTaskException -= UnobserverdTaskException;

            _disposed = true;
            base.Dispose(disposing);
        }

    }
}
