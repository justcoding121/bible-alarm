#nullable enable
using Android.Net;
using Android.App;
using Android.Runtime;
using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.ExoPlayer.Source;
using AndroidX.Media3.DataSource;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.ApplicationModel;
using Serilog;
using System.Reflection;
using System.Threading.Tasks;

namespace Bible.Alarm.Platforms.Android.Services.Media;

/// <summary>
/// Android-specific service for enabling the Next and Previous buttons in system media controls
/// by creating a multi-item queue in ExoPlayer using ConcatenatingMediaSource.
/// Uses three items: dummy previous, current, and dummy next to enable both navigation buttons.
/// </summary>
public class AndroidPlayerNotificationService : IAndroidPlayerNotificationService, IDisposable
{
    private readonly MediaElement _mediaElement;
    private readonly ILogger _logger;
    private ExoPlayerListener? _exoPlayerListener;
    private IExoPlayer? _currentPlayer;

    public event EventHandler? NextButtonPressed;
    public event EventHandler? PreviousButtonPressed;

    public AndroidPlayerNotificationService(INavigationService navigationService, ILogger logger)
    {
        _mediaElement = navigationService.GetMediaElement();
        _logger = logger;
    }

    /// <summary>
    /// Sets a multi-item queue via ExoPlayer using ConcatenatingMediaSource to enable both Next and Previous buttons.
    /// Uses three distinct MediaItems (dummy previous, current, dummy next) with different MediaIds and URI fragments pointing to the same file.
    /// This creates a proper multi-item timeline that MediaSessionConnector recognizes,
    /// unlike duplicate MediaItems which ExoPlayer may deduplicate.
    /// </summary>
    public void SetSourceWithDummyQueue(string uri)
    {
        try
        {
            // Get ExoPlayer instance and cast to IExoPlayer interface
            var player = GetExoPlayer() as IExoPlayer;
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
            TryUpdateMediaSessionActions();
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
    private void TryUpdateMediaSessionActions()
    {
        try
        {
            var session = GetMediaSession();
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
            var player = GetExoPlayer();
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
    private object? GetExoPlayer()
    {
        try
        {
            var session = GetMediaSession();
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
    private object? GetMediaSession()
    {
        try
        {
            // Access MediaElement's handler
            var handler = _mediaElement.Handler;
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
    /// Fires the NextButtonPressed event.
    /// </summary>
    internal void OnNextButtonPressed()
    {
        NextButtonPressed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Fires the PreviousButtonPressed event.
    /// </summary>
    internal void OnPreviousButtonPressed()
    {
        PreviousButtonPressed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        try
        {
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

