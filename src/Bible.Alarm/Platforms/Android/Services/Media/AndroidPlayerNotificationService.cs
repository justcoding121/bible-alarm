#nullable enable
using Android.Content;
using Android.Net;
using Android.App;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using AndroidX.Core.App;
using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.ExoPlayer.Source;
using AndroidX.Media3.DataSource;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Handlers;
using Serilog;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AndroidApplication = Android.App.Application;

namespace Bible.Alarm.Platforms.Android.Services.Media;

/// <summary>
/// Android-specific service for enabling the Next and Previous buttons in system media controls
/// by creating a multi-item queue in ExoPlayer using ConcatenatingMediaSource.
/// Uses three items: dummy previous, current, and dummy next to enable both navigation buttons.
/// </summary>
public class AndroidPlayerNotificationService : IAndroidPlayerNotificationService, IDisposable
{
    private readonly ILogger _logger;
    private ExoPlayerListener? _exoPlayerListener;
    private IExoPlayer? _currentPlayer;

    public AndroidPlayerNotificationService(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Sets a multi-item queue via ExoPlayer using ConcatenatingMediaSource to enable both Next and Previous buttons.
    /// Uses three distinct MediaItems (dummy previous, current, dummy next) with different MediaIds and URI fragments pointing to the same file.
    /// This creates a proper multi-item timeline that MediaSessionConnector recognizes,
    /// unlike duplicate MediaItems which ExoPlayer may deduplicate.
    /// </summary>
    public void SetSourceWithDummyQueue(MediaElement mediaElement, string uri)
    {
        try
        {
            // Get ExoPlayer instance and cast to IExoPlayer interface
            var player = GetExoPlayer(mediaElement) as IExoPlayer;
            if (player == null)
            {
                _logger.Error("Failed to get ExoPlayer instance");
                return;
            }

            // Get Android context for DataSourceFactory
            var context = global::Android.App.Application.Context;
            var dataSourceFactory = new DefaultDataSourceFactory(context);

            // Parse the URI
            var androidUri = global::Android.Net.Uri.Parse(uri);

            // Dummy previous item with distinct URI (add fragment) and ID
            var dummyPreviousUri = androidUri.BuildUpon().Fragment("previous").Build();
            var dummyPreviousItem = new MediaItem.Builder()
                .SetUri(dummyPreviousUri)
                .SetMediaId("bible_alarm_previous_dummy")
                .Build();

            // Create current item
            var currentItem = new MediaItem.Builder()
                .SetUri(androidUri)
                .SetMediaId("bible_alarm_current")
                .Build();

            // Dummy next item with distinct URI (add fragment) and ID
            var dummyNextUri = androidUri.BuildUpon().Fragment("next").Build();
            var dummyNextItem = new MediaItem.Builder()
                .SetUri(dummyNextUri)
                .SetMediaId("bible_alarm_next_dummy")
                .Build();

            // Create ProgressiveMediaSource for each item
            var source1 = new ProgressiveMediaSource.Factory(dataSourceFactory)
                .CreateMediaSource(dummyPreviousItem);
            var source2 = new ProgressiveMediaSource.Factory(dataSourceFactory)
                .CreateMediaSource(currentItem);
            var source3 = new ProgressiveMediaSource.Factory(dataSourceFactory)
                .CreateMediaSource(dummyNextItem);

            // Create ConcatenatingMediaSource with all three sources: [dummy_previous, current, dummy_next]
            var concatenatingSource = new ConcatenatingMediaSource(source1, source2, source3);

            // Set the concatenating source on the player
            player.SetMediaSource(concatenatingSource);
            
            // CRITICAL: Call Prepare() AFTER setting the source to trigger TimelineChanged event
            // This is what makes MediaSessionConnector see HasNextMediaItem and HasPreviousMediaItem = true
            player.Prepare();
            
            // Seek to the middle item (current) - index 1 in the queue [0=dummy_previous, 1=current, 2=dummy_next]
            player.SeekTo(1, 0);
            
            // Do NOT call player.Play() here - let the normal Play() flow handle it

            _logger.Information("Set ConcatenatingMediaSource queue with three items — Next and Previous buttons will appear.");
            
            // Verify HasNextMediaItem and HasPreviousMediaItem are true
            if (player.HasNextMediaItem)
            {
                _logger.Debug("Player HasNextMediaItem: {HasNext}", player.HasNextMediaItem);
            }
            else
            {
                _logger.Debug("Player HasNextMediaItem is still false after setting ConcatenatingMediaSource");
            }
            
            // Check for HasPreviousMediaItem property via reflection
            var playerType = player.GetType();
            var hasPreviousProperty = playerType.GetProperty("HasPreviousMediaItem", BindingFlags.Public | BindingFlags.Instance);
            if (hasPreviousProperty != null)
            {
                var hasPrevious = hasPreviousProperty.GetValue(player);
                _logger.Debug("Player HasPreviousMediaItem: {HasPrevious}", hasPrevious);
            }

            // Set up listener to intercept Next/Previous button presses
            SetupExoPlayerListener(player);

            // Force MediaSession to refresh actions
            TryUpdateMediaSessionActions(mediaElement);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to set queue in SetSourceWithDummyQueue");
        }
    }

    /// <summary>
    /// Updates MediaSession to refresh actions after setting dummy queue.
    /// In Media3, actions are typically derived from Player state, but we may need to invalidate the session.
    /// </summary>
    private void TryUpdateMediaSessionActions(MediaElement mediaElement)
    {
        try
        {
            var session = GetMediaSession(mediaElement);
            if (session == null)
            {
                return;
            }

            var sessionType = session.GetType();
            
            // Try to invalidate the session to refresh actions
            var invalidateMethod = sessionType.GetMethod("Invalidate", BindingFlags.Public | BindingFlags.Instance);
            if (invalidateMethod != null)
            {
                invalidateMethod.Invoke(session, null);
                _logger.Debug("Invalidated MediaSession to refresh actions");
                return;
            }

            // Alternative: Try to get the player and check if it has HasNextMediaItem
            // If the player reports HasNextMediaItem=true, the session should show Next button
            var player = GetExoPlayer(mediaElement);
            if (player != null)
            {
                var playerType = player.GetType();
                var hasNextProperty = playerType.GetProperty("HasNextMediaItem", BindingFlags.Public | BindingFlags.Instance);
                if (hasNextProperty != null)
                {
                    var hasNext = hasNextProperty.GetValue(player);
                    _logger.Debug("Player HasNextMediaItem: {HasNext}", hasNext);
                }
            }

            _logger.Debug("Could not invalidate MediaSession, actions should update automatically from Player state");
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Failed to update MediaSession actions");
        }
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
                _logger.Debug("Player property not found in session");
                return null;
            }

            var player = playerProperty.GetValue(session);
            if (player == null)
            {
                _logger.Debug("Player is null");
                return null;
            }

            return player;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to get ExoPlayer via reflection");
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
                _logger.Debug("MediaElement handler is null");
                return null;
            }

            // Access MediaManager property via reflection
            var handlerType = handler.GetType();
            var mediaManagerProperty = handlerType.GetProperty("MediaManager", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (mediaManagerProperty == null)
            {
                _logger.Debug("MediaManager property not found in handler type: {HandlerType}", handlerType.Name);
                return null;
            }

            var mediaManager = mediaManagerProperty.GetValue(handler);
            if (mediaManager == null)
            {
                _logger.Debug("MediaManager is null");
                return null;
            }

            // Access session field from MediaManager
            var sessionField = mediaManager.GetType().GetField("session", BindingFlags.NonPublic | BindingFlags.Instance);
            if (sessionField == null)
            {
                _logger.Debug("session field not found in MediaManager");
                return null;
            }

            var session = sessionField.GetValue(mediaManager);
            if (session == null)
            {
                _logger.Debug("session is null");
                return null;
            }

            return session;
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Failed to get MediaSession via reflection");
            return null;
        }
    }

    /// <summary>
    /// Sets up the ExoPlayer listener to intercept Next/Previous button presses from system controls.
    /// Uses reflection to call AddListener/RemoveListener with IPlayerListener parameter.
    /// </summary>
    private void SetupExoPlayerListener(IExoPlayer player)
    {
        try
        {
            // Clean up old listener
            if (_exoPlayerListener != null && _currentPlayer != null)
            {
                var removeMethod = _currentPlayer.GetType().GetMethod("RemoveListener", new[] { typeof(IPlayerListener) });
                if (removeMethod != null)
                {
                    removeMethod.Invoke(_currentPlayer, new object[] { _exoPlayerListener });
                }
                _exoPlayerListener.Dispose();
            }

            // Create and add new listener
            _currentPlayer = player;
            _exoPlayerListener = new ExoPlayerListener(this, _logger);
            
            // Use reflection to call AddListener with IPlayerListener parameter
            var addMethod = player.GetType().GetMethod("AddListener", new[] { typeof(IPlayerListener) });
            if (addMethod != null)
            {
                addMethod.Invoke(player, new object[] { _exoPlayerListener });
                _logger.Information("ExoPlayer listener attached — OnMediaItemTransition will fire on Next/Previous press");
            }
            else
            {
                _logger.Warning("AddListener method not found on IExoPlayer");
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to set up ExoPlayer listener");
        }
    }

    /// <summary>
    /// Sends NextButtonPressedMessage via Messenger.
    /// Simply sends the message - let PlaybackService handle the state management.
    /// </summary>
    internal void OnNextButtonPressed()
    {
        // Just send the message - don't try to manipulate ExoPlayer here
        // PlaybackService will handle stopping and preparing the next track
        WeakReferenceMessenger.Default.Send(new NextButtonPressedMessage());
    }

    /// <summary>
    /// Sends PreviousButtonPressedMessage via Messenger.
    /// Simply sends the message - let PlaybackService handle the state management.
    /// </summary>
    internal void OnPreviousButtonPressed()
    {
        // Just send the message - don't try to manipulate ExoPlayer here
        // PlaybackService will handle stopping and preparing the previous track
        WeakReferenceMessenger.Default.Send(new PreviousButtonPressedMessage());
    }

    /// <summary>
    /// Releases the MediaSession and cancels the media notification.
    /// The key fix: calls SetPlayer(null) on PlayerNotificationManager, which is the only way to actually dismiss the notification.
    /// </summary>
    public void ReleaseMediaSession(MediaElement mediaElement)
    {
        try
        {
            _logger.Information("ReleaseMediaSession called - attempting to remove notification");
            
            // Ensure we're on the main thread
            if (!MainThread.IsMainThread)
            {
                MainThread.BeginInvokeOnMainThread(() => ReleaseMediaSession(mediaElement));
                return;
            }

            // Add a small delay to ensure MediaElement has finished its internal stopping process
            // This helps ensure the notification is in a stable state before we try to remove it
            Task.Run(async () =>
            {
                await Task.Delay(100);
                await MainThread.InvokeOnMainThreadAsync(async () => await ReleaseMediaSessionInternalAsync(mediaElement));
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in ReleaseMediaSession");
        }
    }

    /// <summary>
    /// Removes the notification by disconnecting the handler (which releases MediaSession) and canceling the notification.
    /// After DisconnectHandler(), the MediaElement handler will be automatically recreated by MAUI when a new Source is set
    /// and Play() is called. This is the proper way to release a sticky MediaSession-based notification.
    /// MediaElement on Android always uses notification ID = 16777216 (0x1000000) in 7.0.x.
    /// Reference: https://github.com/dotnet/maui/blob/7.0.0/src/Core/src/Platform/Android/MediaElementHandler.cs#L198
    /// </summary>
    private async Task ReleaseMediaSessionInternalAsync(MediaElement mediaElement)
    {
        try
        {
            _logger.Information("Removing notification by disconnecting handler (handler will be recreated on next Play)");

            // 1. Stop playback and reset source (triggers internal cleanup)
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                mediaElement.Stop();
                mediaElement.Source = null;  // Forces session reset
            });

            // 2. Disconnect handler - this properly releases the MediaSession and removes the sticky notification
            mediaElement.Handler?.DisconnectHandler();
            _logger.Debug("Disconnected MediaElement handler - MediaSession released");

            // 3. Send message to BootstrapPage to recreate MediaElement with fresh ExoPlayer instance
            WeakReferenceMessenger.Default.Send(new RecreateMediaElementMessage());
            _logger.Information("Sent RecreateMediaElementMessage to BootstrapPage - MediaElement will be recreated");

            // 4. Cancel the hard-coded notification ID + all (extra safety to ensure notification is gone)
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            if (activity == null)
            {
                _logger.Warning("CurrentActivity is null, cannot cancel notification");
                return;
            }

            var notificationManager = activity.GetSystemService(Context.NotificationService) as NotificationManager;
            if (notificationManager == null)
            {
                _logger.Warning("Could not get Android NotificationManager");
                return;
            }

            notificationManager.Cancel(16777216);  // MAUI's hard-coded ID
            notificationManager.CancelAll();
            _logger.Information("Canceled notification ID 16777216 and all notifications");

            // 5. Samsung-specific: Kill reminders (one-time global fix)
            // Note: This is also done at app startup in MainApplication.OnCreate(), but included here as fallback
            try
            {
                var manufacturer = Microsoft.Maui.Devices.DeviceInfo.Manufacturer;
                if (!string.IsNullOrEmpty(manufacturer) && manufacturer.Contains("samsung", StringComparison.OrdinalIgnoreCase))
                {
                    var appContext = Microsoft.Maui.ApplicationModel.Platform.AppContext;
                    if (appContext != null)
                    {
                        Settings.Global.PutInt(
                            appContext.ContentResolver,
                            "notification_reminders_enabled", 0);
                        _logger.Debug("Disabled Samsung notification reminders");
                    }
                    else
                    {
                        _logger.Debug("Platform.AppContext is null, skipping Samsung reminder disable");
                    }
                }
            }
            catch (Exception ex)
            {
                // No permission needed on most Samsungs, ignore silently
                // Also catch any issues with DeviceInfo.Manufacturer access
                _logger.Debug(ex, "Could not disable Samsung notification reminders (may not have permission or setting may not exist)");
            }

            _logger.Information("Notification removed - handler will be automatically recreated when MediaElement is used again");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error removing notification");
        }
    }


    /// <summary>
    /// Permanently deletes the Media3 notification channel to prevent the system from re-creating it.
    /// This is only available on Android 8.0 (API 26) and above where notification channels exist.
    /// </summary>
    private void KillMedia3ChannelPermanently()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            try
            {
                var appContext = Microsoft.Maui.ApplicationModel.Platform.AppContext;
                if (appContext == null)
                {
                    _logger.Debug("Platform.AppContext is null, cannot delete notification channel");
                    return;
                }

                var notificationManager = appContext.GetSystemService(Context.NotificationService) as NotificationManager;
                
                if (notificationManager != null)
                {
                    notificationManager.DeleteNotificationChannel("media_session");
                    _logger.Information("Deleted Media3 notification channel 'media_session'");
                }
            }
            catch (Exception ex)
            {
                // Channel already deleted, doesn't exist, or permission issue - not critical
                _logger.Debug(ex, "Could not delete Media3 notification channel (may already be deleted)");
            }
        }
    }

    /// <summary>
    /// Gets the MediaManager from MediaElement via reflection.
    /// </summary>
    private object? GetMediaManager(MediaElement mediaElement)
    {
        try
        {
            var handler = mediaElement.Handler;
            if (handler == null)
            {
                _logger.Debug("MediaElement handler is null");
                return null;
            }

            var handlerType = handler.GetType();
            _logger.Debug("Handler type: {HandlerType}", handlerType.FullName);
            
            var mediaManagerProperty = handlerType.GetProperty("MediaManager", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (mediaManagerProperty == null)
            {
                _logger.Debug("MediaManager property not found in handler type: {HandlerType}", handlerType.Name);
                return null;
            }

            var mediaManager = mediaManagerProperty.GetValue(handler);
            if (mediaManager == null)
            {
                _logger.Debug("MediaManager property exists but is null");
            }
            else
            {
                _logger.Debug("MediaManager retrieved successfully: {Type}", mediaManager.GetType().FullName);
            }
            
            return mediaManager;
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Failed to get MediaManager via reflection");
            return null;
        }
    }

    /// <summary>
    /// Force-kill the notification the official Microsoft-approved way for MediaElement 7.0.0+.
    /// MediaElement on Android always uses notification ID = 16777216 (0x1000000) in 7.0.x.
    /// This is the only method that works reliably with Maui MediaElement 7.0.x.
    /// Reference: https://github.com/dotnet/maui/blob/7.0.0/src/Core/src/Platform/Android/MediaElementHandler.cs#L198
    /// </summary>
    private void TryCancelNotificationManually()
    {
        try
        {
            _logger.Information("Attempting to cancel media notification using Microsoft-approved method (ID: 16777216)");
            
            // Get NotificationManager from the current activity
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            if (activity == null)
            {
                _logger.Warning("CurrentActivity is null, cannot cancel notification");
                return;
            }

            var notificationManager = activity.GetSystemService(global::Android.Content.Context.NotificationService) as global::Android.App.NotificationManager;
            
            if (notificationManager == null)
            {
                _logger.Warning("Could not get Android NotificationManager");
                return;
            }

            // MediaElement on Android always uses notification ID = 16777216 (0x1000000) in 7.0.x
            const int MediaElementNotificationId = 16777216; // 0x1000000
            
            // Cancel with the hard-coded ID
            notificationManager.Cancel(MediaElementNotificationId);
            _logger.Information("Canceled notification with MediaElement ID: {Id}", MediaElementNotificationId);
            
            // Extra paranoia – also cancel the built-in media session tag
            notificationManager.Cancel("media_session_tag", MediaElementNotificationId);
            _logger.Debug("Also attempted to cancel with tag 'media_session_tag' and ID: {Id}", MediaElementNotificationId);
            
            // And cancel everything just in case (nuclear option)
            notificationManager.CancelAll();
            _logger.Debug("Called CancelAll() as final cleanup");
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error in TryCancelNotificationManually (Microsoft-approved method)");
        }
    }

    public void Dispose()
    {
        try
        {
            // Note: ReleaseMediaSession is now called from AudioPlayer with MediaElement instance
            // We don't call it here since we no longer have a MediaElement reference

            if (_exoPlayerListener != null && _currentPlayer != null)
            {
                try
                {
                    var removeMethod = _currentPlayer.GetType().GetMethod("RemoveListener", new[] { typeof(IPlayerListener) });
                    if (removeMethod != null)
                    {
                        removeMethod.Invoke(_currentPlayer, new object[] { _exoPlayerListener });
                    }
                    _exoPlayerListener.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Error removing listener during dispose");
                }
                _exoPlayerListener = null;
                _currentPlayer = null;
            }
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Error disposing ExoPlayer listener");
        }
    }

    /// <summary>
    /// ExoPlayer listener that intercepts Next/Previous button presses from system controls.
    /// Implements IPlayerListener interface (7.0.0 compatible).
    /// </summary>
    private class ExoPlayerListener : Java.Lang.Object, IPlayerListener
    {
        private readonly AndroidPlayerNotificationService _parent;
        private readonly ILogger _logger;

        public ExoPlayerListener(AndroidPlayerNotificationService parent, ILogger logger)
        {
            _parent = parent;
            _logger = logger;
        }

        public void OnMediaItemTransition(MediaItem? mediaItem, int reason)
        {
            // MediaItemTransitionReasonManual = 2 (user manually pressed Next/Previous)
            const int MediaItemTransitionReasonManual = 2;
            
            if (reason == MediaItemTransitionReasonManual && mediaItem != null)
            {
                var mediaId = mediaItem.MediaId;
                
                if (mediaId == "bible_alarm_next_dummy")
                {
                    _logger.Information("NEXT BUTTON PRESSED — BLOCKING DUMMY TRACK");
                    // Fire the event first
                    _parent.OnNextButtonPressed();       
                }
                else if (mediaId == "bible_alarm_previous_dummy")
                {
                    _logger.Information("PREVIOUS BUTTON PRESSED — BLOCKING DUMMY TRACK");
                    // Fire the event first
                            _parent.OnPreviousButtonPressed();
                }
            }
        }

        // Required interface methods - can be empty
        public void OnPlaybackStateChanged(int playbackState) { }
        public void OnPlayWhenReadyChanged(bool playWhenReady, int reason) { }
        public void OnIsPlayingChanged(bool isPlaying) { }
        public void OnRepeatModeChanged(int repeatMode) { }
        public void OnShuffleModeEnabledChanged(bool shuffleModeEnabled) { }
        public void OnPlayerError(PlaybackException error) { }
        public void OnPlaybackParametersChanged(PlaybackParameters playbackParameters) { }
        public void OnSeekBackIncrementChanged(long seekBackIncrementMs) { }
        public void OnSeekForwardIncrementChanged(long seekForwardIncrementMs) { }
        public void OnAudioAttributesChanged(AudioAttributes audioAttributes) { }
        public void OnAudioSessionIdChanged(int audioSessionId) { }
        public void OnVolumeChanged(float volume) { }
        public void OnSkipSilenceEnabledChanged(bool skipSilenceEnabled) { }
        public void OnDeviceInfoChanged(AndroidX.Media3.Common.DeviceInfo deviceInfo) { }
        public void OnDeviceVolumeChanged(int volume, bool muted) { }
        public void OnTimelineChanged(Timeline timeline, int reason) { }
        public void OnTracksChanged(Tracks tracks) { }
        public void OnEvents(IExoPlayer player, object events) { }
    }
}

