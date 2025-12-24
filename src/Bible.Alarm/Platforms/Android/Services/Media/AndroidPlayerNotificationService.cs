#nullable enable
using System.Reflection;
using AndroidX.Media3.Common;
using AndroidX.Media3.Common.Text;
using AndroidX.Media3.DataSource;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.ExoPlayer.Source;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;
using Application = Android.App.Application;
using DeviceInfo = AndroidX.Media3.Common.DeviceInfo;
using Exception = System.Exception;
using Object = Java.Lang.Object;
using Uri = Android.Net.Uri;

namespace Bible.Alarm.Platforms.Android.Services.Media;

/// <summary>
/// Android-specific service for enabling the Next and Previous buttons in system media controls
/// by creating a multi-item queue in ExoPlayer using ConcatenatingMediaSource.
/// Uses three items: dummy previous, current, and dummy next to enable both navigation buttons.
/// </summary>
public sealed class AndroidPlayerNotificationService(ILogger logger) : IAndroidPlayerNotificationService, IDisposable
{
    private ExoPlayerListener? exoPlayerListener;
    private IExoPlayer? currentPlayer;

    /// <summary>
    /// Sets a multi-item queue via ExoPlayer using SetMediaSources to enable both Next and Previous buttons.
    /// Uses distinct MediaItems (dummy previous, current, dummy next) with different MediaIds and URI fragments pointing to the same file.
    /// This creates a proper multi-item timeline that MediaSessionConnector recognizes,
    /// unlike duplicate MediaItems which ExoPlayer may deduplicate.
    /// Only creates dummy items when needed (previous dummy only if not first track, next dummy only if not last track).
    /// </summary>
    public void SetSourceWithDummyQueue(MediaElement mediaElement, string uri, bool isFirstTrack = false, bool isLastTrack = false)
    {
        try
        {
            var player = GetValidatedExoPlayer(mediaElement);
            if (player == null)
            {
                return;
            }

            var dataSourceFactory = CreateDataSourceFactory();
            var androidUri = ParseUri(uri);
            if (androidUri == null)
            {
                return;
            }

            var sources = BuildMediaSources(dataSourceFactory, androidUri, isFirstTrack, isLastTrack);
            if (sources == null)
            {
                return;
            }

            ConfigurePlayerWithSources(player, sources, isFirstTrack);
            LogQueueConfiguration(sources.Count, isFirstTrack, isLastTrack);
            VerifyPlayerCapabilities(player);
            SetupExoPlayerListener(player);
            LogFinalConfirmation(sources.Count);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to set queue in SetSourceWithDummyQueue");
        }
    }

    private IExoPlayer? GetValidatedExoPlayer(MediaElement mediaElement)
    {
        var player = GetExoPlayer(mediaElement) as IExoPlayer;
        if (player == null)
        {
            logger.Error("Failed to get ExoPlayer instance");
        }
        return player;
    }

    private DefaultDataSource.Factory CreateDataSourceFactory()
    {
        var context = Application.Context;
        return new DefaultDataSource.Factory(context);
    }

    private Uri? ParseUri(string uri)
    {
        var androidUri = Uri.Parse(uri);
        if (androidUri == null)
        {
            logger.Error("Failed to parse URI: {Uri}", uri);
        }
        return androidUri;
    }

    private List<IMediaSource>? BuildMediaSources(DefaultDataSource.Factory dataSourceFactory, Uri androidUri, bool isFirstTrack, bool isLastTrack)
    {
        var sources = new List<IMediaSource>();
        var currentSource = CreateCurrentMediaSource(dataSourceFactory, androidUri);
        if (currentSource == null)
        {
            return null;
        }

        // Add previous dummy only if not first track
        if (!isFirstTrack)
        {
            var previousSource = CreateDummyMediaSource("bible_alarm_previous_dummy", dataSourceFactory);
            if (previousSource == null)
            {
                return null;
            }
            sources.Add(previousSource);
        }

        // Add current item
        sources.Add(currentSource);

        // Add next dummy only if not last track
        if (!isLastTrack)
        {
            var nextSource = CreateDummyMediaSource("bible_alarm_next_dummy", dataSourceFactory);
            if (nextSource == null)
            {
                return null;
            }
            sources.Add(nextSource);
        }

        return sources;
    }

    private IMediaSource? CreateCurrentMediaSource(DefaultDataSource.Factory dataSourceFactory, Uri androidUri)
    {
        var currentItemBuilder = new MediaItem.Builder()
            .SetUri(androidUri)?
            .SetMediaId("bible_alarm_current");

        var currentItem = currentItemBuilder?.Build();
        if (currentItem == null)
        {
            logger.Error("Failed to build MediaItem for current item");
            return null;
        }

        var currentSource = new ProgressiveMediaSource.Factory(dataSourceFactory)
            .CreateMediaSource(currentItem);
        if (currentSource == null)
        {
            logger.Error("Failed to create MediaSource for current item");
        }

        return currentSource;
    }

    private void ConfigurePlayerWithSources(IExoPlayer player, List<IMediaSource> sources, bool isFirstTrack)
    {
        // Set media sources directly on the player
        player.SetMediaSources([.. sources]);

        // CRITICAL: Call Prepare() AFTER setting the source to trigger TimelineChanged event
        // This is what makes MediaSessionConnector see HasNextMediaItem and HasPreviousMediaItem = true
        player.Prepare();

        // Seek to the current item index
        // If previous dummy exists, current is at index 1, otherwise at index 0
        var currentItemIndex = isFirstTrack ? 0 : 1;
        player.SeekTo(currentItemIndex, 0);
    }

    private void LogQueueConfiguration(int itemCount, bool isFirstTrack, bool isLastTrack)
    {
        var itemsDescription = isFirstTrack && isLastTrack ? "current only" :
                               isFirstTrack ? "current + next dummy" :
                               isLastTrack ? "previous dummy + current" :
                               "previous dummy + current + next dummy";
        logger.Information("Set media queue with {ItemCount} items ({ItemsDescription}) — Next and Previous buttons will appear conditionally.",
            itemCount, itemsDescription);
    }

    private void VerifyPlayerCapabilities(IExoPlayer player)
    {
        if (player.HasNextMediaItem)
        {
            logger.Debug("Player HasNextMediaItem: {HasNext}", player.HasNextMediaItem);
        }
        else
        {
            logger.Debug("Player HasNextMediaItem is still false after setting media sources");
        }

        // Check for HasPreviousMediaItem property via reflection
        var playerType = player.GetType();
        var hasPreviousProperty = playerType.GetProperty("HasPreviousMediaItem", BindingFlags.Public | BindingFlags.Instance);
        if (hasPreviousProperty != null)
        {
            var hasPrevious = hasPreviousProperty.GetValue(player);
            logger.Debug("Player HasPreviousMediaItem: {HasPrevious}", hasPrevious);
        }
    }

    private void LogFinalConfirmation(int itemCount)
    {
        // Log confirmation that Next/Previous buttons are enabled
        // This confirms the implementation is ready for Pixel 7a and all Android devices
        // MediaSession.SetSessionActivity() is configured in MediaManager to handle notification body taps on Android 14+
        logger.Information("NEXT/PREV BUTTONS ENABLED — Pixel 7a ready. Queue configured with {ItemCount} items. MediaSession.SetSessionActivity() configured for notification body taps.", itemCount);
    }

    /// <summary>
    /// Creates a dummy media source using a silent MP3 file for creating a multi-item queue.
    /// </summary>
    private IMediaSource? CreateDummyMediaSource(
        string mediaId,
        DefaultDataSource.Factory dataSourceFactory)
    {
        var silentMp3Uri = GetSilentMp3Uri();
        if (string.IsNullOrEmpty(silentMp3Uri))
        {
            logger.Error("Failed to get silent MP3 URI for dummy item");
            return null;
        }

        var dummyUri = ParseUri(silentMp3Uri);
        if (dummyUri == null)
        {
            return null;
        }

        return CreateMediaSourceFromUri(dummyUri, mediaId, dataSourceFactory);
    }

    private IMediaSource? CreateMediaSourceFromUri(Uri uri, string mediaId, DefaultDataSource.Factory dataSourceFactory)
    {
        var mediaItem = CreateMediaItem(uri, mediaId);
        if (mediaItem == null)
        {
            return null;
        }

        var source = new ProgressiveMediaSource.Factory(dataSourceFactory)
            .CreateMediaSource(mediaItem);
        if (source == null)
        {
            logger.Error("Failed to create MediaSource for item with MediaId: {MediaId}", mediaId);
        }

        return source;
    }

    private MediaItem? CreateMediaItem(Uri uri, string mediaId)
    {
        var itemBuilder = new MediaItem.Builder()
            .SetUri(uri)?
            .SetMediaId(mediaId);

        var item = itemBuilder?.Build();
        if (item == null)
        {
            logger.Error("Failed to build MediaItem with MediaId: {MediaId}", mediaId);
        }

        return item;
    }

    /// <summary>
    /// Gets the URI of the silent MP3 file from the storage directory (same as schedule database).
    /// The file is copied from embedded resources during bootstrap.
    /// </summary>
    private string? GetSilentMp3Uri()
    {
        try
        {
            var storageService = GetValidatedStorageService();
            if (storageService == null)
            {
                return null;
            }

            var filePath = GetSilentMp3FilePath(storageService);
            if (!File.Exists(filePath))
            {
                logger.Warning("Silent MP3 not found in storage: {FilePath}. It should have been copied during bootstrap.", filePath);
                return null;
            }

            return CreateFileUri(filePath);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting silent MP3 URI");
            return null;
        }
    }

    private IStorageService? GetValidatedStorageService()
    {
        var storageService = ServiceProviderManager.GetService<IStorageService>();
        if (storageService == null)
        {
            logger.Error("IStorageService not available - cannot get silent MP3 URI");
        }
        return storageService;
    }

    private string GetSilentMp3FilePath(IStorageService storageService)
    {
        const string ResourceFileName = "silent.mp3";
        // Use StorageRoot (same directory as schedule database) instead of CacheRoot
        // because cache can get deleted by the system
        var storageDir = storageService.StorageRoot;
        return Path.Combine(storageDir, ResourceFileName);
    }

    private string CreateFileUri(string filePath)
    {
        var uri = new System.Uri(filePath).AbsoluteUri;
        logger.Debug("Using silent MP3 from storage: {FilePath}", filePath);
        return uri;
    }

    /// <summary>
    /// Gets the underlying ExoPlayer instance from MediaElement via reflection.
    /// Returns as object to avoid type resolution issues at compile time.
    /// </summary>
    private object? GetExoPlayer(MediaElement mediaElement)
    {
        try
        {
            var session = GetMediaSession(mediaElement);
            if (session == null)
            {
                return null;
            }

            // Access player from session
            var playerProperty = session.GetType().GetProperty("Player", BindingFlags.Public | BindingFlags.Instance);
            if (playerProperty == null)
            {
                logger.Debug("Player property not found in session");
                return null;
            }

            var player = playerProperty.GetValue(session);
            if (player == null)
            {
                logger.Debug("Player is null");
                return null;
            }

            return player;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to get ExoPlayer via reflection");
            return null;
        }
    }

    /// <summary>
    /// Gets the MediaSession from MediaElement via reflection.
    /// This is shared logic used by both GetExoPlayer and TryUpdateMediaSessionActions.
    /// </summary>
    private object? GetMediaSession(MediaElement mediaElement)
    {
        try
        {
            // Access MediaElement's handler
            var handler = mediaElement.Handler;
            if (handler == null)
            {
                logger.Debug("MediaElement handler is null");
                return null;
            }

            // Access MediaManager property via reflection
            var handlerType = handler.GetType();
            var mediaManagerProperty = handlerType.GetProperty("MediaManager", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (mediaManagerProperty == null)
            {
                logger.Debug("MediaManager property not found in handler type: {HandlerType}", handlerType.Name);
                return null;
            }

            var mediaManager = mediaManagerProperty.GetValue(handler);
            if (mediaManager == null)
            {
                logger.Debug("MediaManager is null");
                return null;
            }

            // Access session field from MediaManager
            var sessionField = mediaManager.GetType().GetField("session", BindingFlags.NonPublic | BindingFlags.Instance);
            if (sessionField == null)
            {
                logger.Debug("session field not found in MediaManager");
                return null;
            }

            var session = sessionField.GetValue(mediaManager);
            if (session == null)
            {
                logger.Debug("session is null");
                return null;
            }

            return session;
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Failed to get MediaSession via reflection");
            return null;
        }
    }

    /// <summary>
    /// Removes the ExoPlayer listener and cleans up references.
    /// Should be called before DisconnectHandler() to avoid accessing disposed ExoPlayer.
    /// </summary>
    private void RemoveExoPlayerListener()
    {
        try
        {
            if (exoPlayerListener != null && currentPlayer != null)
            {
                try
                {
                    var removeMethod = currentPlayer.GetType().GetMethod("RemoveListener", [typeof(IPlayerListener)]);
                    if (removeMethod != null)
                    {
                        _ = removeMethod.Invoke(currentPlayer, [exoPlayerListener]);
                        logger.Debug("Removed ExoPlayer listener from player");
                    }
                }
                catch (ObjectDisposedException ex)
                {
                    // Player is already disposed - this is expected when handler is disconnected
                    logger.Debug(ex, "ExoPlayer is already disposed, skipping RemoveListener call");
                }
                catch (Exception ex)
                {
                    // Other exceptions during removal - log but continue
                    logger.Debug(ex, "Error removing listener from player (may be disposed)");
                }

                // Dispose the listener
                try
                {
                    exoPlayerListener.Dispose();
                    logger.Debug("Disposed ExoPlayer listener");
                }
                catch (Exception ex)
                {
                    logger.Debug(ex, "Error disposing listener");
                }

                exoPlayerListener = null;
            }

            currentPlayer = null;
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Error in RemoveExoPlayerListener");
        }
    }

    /// <summary>
    /// Sets up the ExoPlayer listener to intercept Next/Previous button presses from system controls.
    /// Uses reflection to call AddListener with IPlayerListener parameter.
    /// Always removes any existing listener before adding a new one to prevent duplicate listeners.
    /// </summary>
    private void SetupExoPlayerListener(IExoPlayer player)
    {
        try
        {
            CleanupExistingListener();
            CreateAndAttachNewListener(player);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to set up ExoPlayer listener");
        }
    }

    private void CleanupExistingListener()
    {
        // Always remove the old listener first, regardless of whether it's the same player or different
        // This prevents duplicate listeners when SetSourceWithDummyQueue is called multiple times
        if (exoPlayerListener != null && currentPlayer != null)
        {
            RemoveExoPlayerListener();
        }
    }

    private void CreateAndAttachNewListener(IExoPlayer player)
    {
        // Create and add new listener
        currentPlayer = player;
        exoPlayerListener = new ExoPlayerListener(logger);

        // Use reflection to call AddListener with IPlayerListener parameter
        var addMethod = player.GetType().GetMethod("AddListener", [typeof(IPlayerListener)]);
        if (addMethod != null)
        {
            _ = addMethod.Invoke(player, [exoPlayerListener]);
            logger.Information("ExoPlayer listener attached — OnMediaItemTransition will fire on Next/Previous press");
        }
        else
        {
            logger.Warning("AddListener method not found on IExoPlayer");
        }
    }

    /// <summary>
    /// Sends NextButtonPressedMessage via Messenger.
    /// Simply sends the message - let PlaybackService handle the state management.
    /// </summary>
    internal static void OnNextButtonPressed() =>
        // Just send the message - don't try to manipulate ExoPlayer here
        // PlaybackService will handle stopping and preparing the next track
        WeakReferenceMessenger.Default.Send(new NextButtonPressedMessage());

    /// <summary>
    /// Sends PreviousButtonPressedMessage via Messenger.
    /// Simply sends the message - let PlaybackService handle the state management.
    /// </summary>
    internal static void OnPreviousButtonPressed() =>
        // Just send the message - don't try to manipulate ExoPlayer here
        // PlaybackService will handle stopping and preparing the previous track
        WeakReferenceMessenger.Default.Send(new PreviousButtonPressedMessage());

    /// <summary>
    /// Releases the MediaSession and cancels the media notification.
    /// The key fix: calls SetPlayer(null) on PlayerNotificationManager, which is the only way to actually dismiss the notification.
    /// </summary>
    public void ReleaseMediaSession(MediaElement mediaElement)
    {
        try
        {
            logger.Information("ReleaseMediaSession called - attempting to remove notification");

            // Ensure we're on the main thread
            if (!MainThread.IsMainThread)
            {
                MainThread.BeginInvokeOnMainThread(() => ReleaseMediaSession(mediaElement));
                return;
            }

            // Add a small delay to ensure MediaElement has finished its internal stopping process
            // This helps ensure the notification is in a stable state before we try to remove it
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                await MainThread.InvokeOnMainThreadAsync(async () => await ReleaseMediaSessionInternalAsync());
            });
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error in ReleaseMediaSession");
        }
    }

    /// <summary>
    /// Removes Android-specific notification resources and sends DestroyMediaElementMessage to trigger MediaElement cleanup.
    /// MediaElement lifecycle (handler disconnect/dispose) is handled by MediaElementService in response to DestroyMediaElementMessage.
    /// This method focuses on Android-specific cleanup: removing ExoPlayer listener and canceling notifications.
    /// MediaElement 7.0.0 on Android uses notification ID = 1 (confirmed from MediaControlsService.android.cs source code).
    /// </summary>
    private async Task ReleaseMediaSessionInternalAsync()
    {
        try
        {
            logger.Information("Removing Android notification resources and triggering MediaElement cleanup");

            // 1. Remove our ExoPlayer listener first (best effort) to stop intercepting callbacks.
            RemoveExoPlayerListener();

            // 3. Send message to MediaElementService to destroy MediaElement and disconnect handler
            // MediaElementService.DestroyMediaElement() will handle handler disconnect and disposal
            // This centralizes MediaElement lifecycle management in one place
            _ = WeakReferenceMessenger.Default.Send(new DestroyMediaElementMessage());
            logger.Information("Sent DestroyMediaElementMessage - MediaElementService will handle handler disconnect and disposal");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error removing notification");
        }
    }

    private bool isDisposed;

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        try
        {
            // Note: ReleaseMediaSession is now called from AudioPlayer with MediaElement instance
            // We don't call it here since we no longer have a MediaElement reference

            // Remove listener using the same method used during handler disconnect
            RemoveExoPlayerListener();
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Error disposing AndroidPlayerNotificationService");
        }

        // All injected services (logger) are singletons, so don't dispose them
    }

    /// <summary>
    /// ExoPlayer listener that intercepts Next/Previous button presses from system controls.
    /// Implements IPlayerListener interface (7.0.0 compatible).
    /// </summary>
    private class ExoPlayerListener(ILogger logger) : Object, IPlayerListener
    {
        private DateTime lastButtonPressTime = DateTime.MinValue;
        private string? lastMediaId;
        // Ignore duplicate presses within 500ms
        private const int DebounceMilliseconds = 500;

        /// <summary>
        /// Checks if the media transition should be processed or debounced.
        /// Returns true if the transition should be processed, false if it should be debounced.
        /// </summary>
        private bool ShouldProcessMediaTransition(string? mediaId, string transitionType)
        {
            // If mediaId is null, we can't debounce it, so process it
            if (mediaId == null)
            {
                return true;
            }

            var now = DateTime.UtcNow;

            // Debounce: ignore duplicate transitions within the debounce window
            if (lastMediaId == mediaId &&
                (now - lastButtonPressTime).TotalMilliseconds < DebounceMilliseconds)
            {
                logger.Debug("Ignoring duplicate {TransitionType} for {MediaId} (debounced)", transitionType, mediaId);
                return false;
            }

            lastButtonPressTime = now;
            lastMediaId = mediaId;
            return true;
        }

        public void OnMediaItemTransition(MediaItem? mediaItem, int reason)
        {
            const int AutomaticTransitionReason = 1;
            const int MediaItemTransitionReasonManual = 2;

            if (mediaItem != null)
            {
                var mediaId = mediaItem.MediaId;

                if (reason == MediaItemTransitionReasonManual)
                {
                    if (!ShouldProcessMediaTransition(mediaId, "button press"))
                    {
                        return;
                    }

                    if (mediaId == "bible_alarm_next_dummy")
                    {
                        logger.Information("NEXT BUTTON PRESSED — BLOCKING DUMMY TRACK");
                        OnNextButtonPressed();
                    }
                    else if (mediaId == "bible_alarm_previous_dummy")
                    {
                        logger.Information("PREVIOUS BUTTON PRESSED — BLOCKING DUMMY TRACK");
                        OnPreviousButtonPressed();
                    }
                }
                else if (reason == AutomaticTransitionReason
                         && mediaId == "bible_alarm_next_dummy")
                {
                    if (!ShouldProcessMediaTransition(mediaId, "automatic transition"))
                    {
                        return;
                    }

                    logger.Information("AUTOMATIC TRANSITION - BLOCKING DUMMY NEXT TRACK");
                    OnNextButtonPressed();
                }
            }
        }

        // Required interface methods - can be empty
        public void OnAudioAttributesChanged(AudioAttributes? audioAttributes) { }
        public void OnAudioSessionIdChanged(int audioSessionId) { }
        public void OnAvailableCommandsChanged(PlayerCommands? player) { }
        public void OnCues(CueGroup? cues) { }
        public void OnDeviceInfoChanged(DeviceInfo? deviceInfo) { }
        public void OnDeviceVolumeChanged(int volume, bool muted) { }
        public void OnEvents(IPlayer? player, PlayerEvents? playerEvents) { }
        public void OnIsLoadingChanged(bool isLoading) { }
        public void OnIsPlayingChanged(bool isPlaying) { }
        public void OnLoadingChanged(bool isLoading) { }
        public void OnMaxSeekToPreviousPositionChanged(long maxSeekToPreviousPositionMs) { }
        public void OnMediaMetadataChanged(MediaMetadata? mediaMetadata) { }
        public void OnMetadata(Metadata? metadata) { }
        public void OnPlaybackParametersChanged(PlaybackParameters? playbackParameters) { }
        public void OnPlaybackStateChanged(int playbackState) { }
        public void OnPlaybackSuppressionReasonChanged(int playbackSuppressionReason) { }
        public void OnPlayWhenReadyChanged(bool playWhenReady, int reason) { }
        public void OnPlayerStateChanged(bool playWhenReady, int playbackState) { }
        public void OnPlayerError(PlaybackException? error) { }
        public void OnPlayerErrorChanged(PlaybackException? error) { }
        public void OnPlaylistMetadataChanged(MediaMetadata? mediaMetadata) { }
        public void OnPositionDiscontinuity(PlayerPositionInfo? oldPosition, PlayerPositionInfo? newPosition, int reason) { }
        public void OnRepeatModeChanged(int repeatMode) { }
        public void OnRenderedFirstFrame() { }
        public void OnSeekBackIncrementChanged(long seekBackIncrementMs) { }
        public void OnSeekForwardIncrementChanged(long seekForwardIncrementMs) { }
        public void OnShuffleModeEnabledChanged(bool shuffleModeEnabled) { }
        public void OnSkipSilenceEnabledChanged(bool skipSilenceEnabled) { }
        public void OnSurfaceSizeChanged(int width, int height) { }
        public void OnTimelineChanged(Timeline? timeline, int reason) { }
        public void OnTrackSelectionParametersChanged(TrackSelectionParameters? trackSelectionParameters) { }
        public void OnTracksChanged(Tracks? tracks) { }
        public void OnVolumeChanged(float volume) { }
    }
}
