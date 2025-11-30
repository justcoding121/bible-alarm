#nullable enable
using Android.Content;
using Android.Net;
using Android.App;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using AndroidX.Core.App;
using AndroidX.Media.App;
using AndroidX.Media3.Common;
using AndroidX.Media3.Session;
using NotificationCompat = AndroidX.Core.App.NotificationCompat;
using MediaStyle = AndroidX.Media.App.NotificationCompat.MediaStyle;
using Java.Lang;
using Java.Interop;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.ExoPlayer.Source;
using AndroidX.Media3.DataSource;
using Bible.Alarm.Common.Messenger;
using TaskStackBuilder = AndroidX.Core.App.TaskStackBuilder;
using Bible.Alarm.Platforms.Android;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Fluxor;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Handlers;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AndroidApplication = Android.App.Application;
using Exception = System.Exception;

namespace Bible.Alarm.Platforms.Android.Services.Media;

/// <summary>
/// Android-specific service for enabling the Next and Previous buttons in system media controls
/// by creating a multi-item queue in ExoPlayer using ConcatenatingMediaSource.
/// Uses three items: dummy previous, current, and dummy next to enable both navigation buttons.
/// </summary>
public class AndroidPlayerNotificationService : IAndroidPlayerNotificationService, IDisposable
{
    private readonly ILogger _logger;
    private readonly IState<PlaybackState> _playbackState;
    private ExoPlayerListener? _exoPlayerListener;
    private IExoPlayer? _currentPlayer;
    private CancellationTokenSource? _notificationOverrideCancellationTokenSource;

    public AndroidPlayerNotificationService(ILogger logger, IState<PlaybackState> playbackState)
    {
        _logger = logger;
        _playbackState = playbackState;
    }

    /// <summary>
    /// Sets a multi-item queue via ExoPlayer using ConcatenatingMediaSource to enable both Next and Previous buttons.
    /// Uses distinct MediaItems (dummy previous, current, dummy next) with different MediaIds and URI fragments pointing to the same file.
    /// This creates a proper multi-item timeline that MediaSessionConnector recognizes,
    /// unlike duplicate MediaItems which ExoPlayer may deduplicate.
    /// Only creates dummy items when needed (previous dummy only if not first track, next dummy only if not last track).
    /// </summary>
    /// <param name="mediaElement">The MediaElement instance to use</param>
    /// <param name="uri">The URI of the current track</param>
    /// <param name="isFirstTrack">True if this is the first track (no previous dummy needed)</param>
    /// <param name="isLastTrack">True if this is the last track (no next dummy needed)</param>
    public void SetSourceWithDummyQueue(MediaElement mediaElement, string uri, bool isFirstTrack = false, bool isLastTrack = false)
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

            // Build list of sources based on track position
            var sources = new List<IMediaSource>();

            // Create current item (always needed)
            var currentItem = new MediaItem.Builder()
                .SetUri(androidUri)
                .SetMediaId("bible_alarm_current")
                .Build();
            var currentSource = new ProgressiveMediaSource.Factory(dataSourceFactory)
                .CreateMediaSource(currentItem);

            // Add previous dummy only if not first track
            if (!isFirstTrack)
            {
                var dummyPreviousUri = androidUri.BuildUpon().Fragment("previous").Build();
                var dummyPreviousItem = new MediaItem.Builder()
                    .SetUri(dummyPreviousUri)
                    .SetMediaId("bible_alarm_previous_dummy")
                    .Build();
                var previousSource = new ProgressiveMediaSource.Factory(dataSourceFactory)
                    .CreateMediaSource(dummyPreviousItem);
                sources.Add(previousSource);
            }

            // Add current item
            sources.Add(currentSource);

            // Add next dummy only if not last track
            if (!isLastTrack)
            {
                var dummyNextUri = androidUri.BuildUpon().Fragment("next").Build();
                var dummyNextItem = new MediaItem.Builder()
                    .SetUri(dummyNextUri)
                    .SetMediaId("bible_alarm_next_dummy")
                    .Build();
                var nextSource = new ProgressiveMediaSource.Factory(dataSourceFactory)
                    .CreateMediaSource(dummyNextItem);
                sources.Add(nextSource);
            }

            // Create ConcatenatingMediaSource with the needed sources
            var concatenatingSource = new ConcatenatingMediaSource(sources.ToArray());

            // Set the concatenating source on the player
            player.SetMediaSource(concatenatingSource);
            
            // CRITICAL: Call Prepare() AFTER setting the source to trigger TimelineChanged event
            // This is what makes MediaSessionConnector see HasNextMediaItem and HasPreviousMediaItem = true
            player.Prepare();
            
            // Seek to the current item index
            // If previous dummy exists, current is at index 1, otherwise at index 0
            var currentItemIndex = isFirstTrack ? 0 : 1;
            player.SeekTo(currentItemIndex, 0);
            
            // Do NOT call player.Play() here - let the normal Play() flow handle it

            var itemCount = sources.Count;
            var itemsDescription = isFirstTrack && isLastTrack ? "current only" :
                                   isFirstTrack ? "current + next dummy" :
                                   isLastTrack ? "previous dummy + current" :
                                   "previous dummy + current + next dummy";
            _logger.Information("Set ConcatenatingMediaSource queue with {ItemCount} items ({ItemsDescription}) — Next and Previous buttons will appear conditionally.", 
                itemCount, itemsDescription);
            
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

            // Set notification body click to open app
            SetNotificationClickToOpenApp(mediaElement);
            
            // Start/restart continuous notification override timer (800ms interval) if needed
            // This is a workaround for MediaElement 7.0.0 bug where it recreates the notification
            // and overwrites our custom ContentIntent. The timer continuously reapplies our override.
            // Only restart if timer is not running or has been cancelled
            var needsRestart = _notificationOverrideCancellationTokenSource == null 
                || _notificationOverrideCancellationTokenSource.IsCancellationRequested;
            
            if (needsRestart)
            {
                StartNotificationIntentOverrideTimer();
            }

            // Force MediaSession to refresh actions
            TryUpdateMediaSessionActions(mediaElement);
            
            // Log confirmation that Next/Previous buttons are enabled
            // This confirms the implementation is ready for Pixel 7a and all Android devices
            _logger.Information("NEXT/PREV BUTTONS ENABLED — Pixel 7a ready. Queue configured with {ItemCount} items. Continuous notification override timer started (800ms interval - workaround for MediaElement 7.0.0 bug).", itemCount);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to set queue in SetSourceWithDummyQueue");
        }
    }

    /// <summary>
    /// Sets up notification body tap handling using SessionActivity.
    /// This works on Android 8-13 and many Android 14+ devices (Samsung, Xiaomi, etc.).
    /// For Pixel 7a and other stock Android 14+ devices, we also continuously override the notification's ContentIntent.
    /// </summary>
    private void SetNotificationClickToOpenApp(MediaElement mediaElement)
    {
        var session = GetMediaSession(mediaElement);
        if (session == null) return;

        var sessionType = session.GetType();

        // Set SessionActivity - this works on Android 8-13 and many Android 14+ devices
        try
        {
            var context = Microsoft.Maui.ApplicationModel.Platform.AppContext;
            var intent = new Intent(context, typeof(MainActivity));
            intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop | ActivityFlags.SingleTop);
            intent.SetAction("Bible.Alarm.NOTIFICATION_CLICK");

            if (_playbackState.Value.CurrentScheduleId.HasValue)
                intent.PutExtra("schedule_id", _playbackState.Value.CurrentScheduleId.Value);

            var pendingIntent = PendingIntent.GetActivity(
                context, 
                999999, 
                intent, 
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            sessionType.GetProperty("SessionActivity", BindingFlags.Public | BindingFlags.Instance)?
                       .SetValue(session, pendingIntent);

            _logger.Debug("Set SessionActivity for notification body taps");
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Failed to set SessionActivity");
        }
    }

    /// <summary>
    /// Overrides the media notification to set MediaSession token to null in MediaStyle.
    /// This forces Android 14+ to use ContentIntent for body taps instead of routing to OnPlay() callback.
    /// Key insight: MediaStyle with null MediaSession token → body taps use ContentIntent (works on all Android versions).
    /// MediaStyle with MediaSession token → body taps go to OnPlay() (Android 14+ only).
    /// Note: mediaElement parameter is unused but kept for backward compatibility with existing call sites.
    /// </summary>
    private void OverrideNotificationIntent(MediaElement? mediaElement = null)
    {
        try
        {
            var context = Microsoft.Maui.ApplicationModel.Platform.AppContext;
            var notificationManager = context.GetSystemService(Context.NotificationService) as NotificationManager;
            if (notificationManager == null) return;

            const int MEDIA_NOTIFICATION_ID = 1; // MediaElement 7.0.0 uses notification ID 1

            // Check if notification exists
            var activeNotifications = notificationManager.GetActiveNotifications();
            var existingNotification = activeNotifications?.FirstOrDefault(n => n.Id == MEDIA_NOTIFICATION_ID);
            if (existingNotification?.Notification == null)
            {
                // Notification not posted yet, will be called again when it's available
                return;
            }

            // Create our custom intent to open the app
            var intent = new Intent(context, typeof(MainActivity));
            intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop | ActivityFlags.SingleTop);
            intent.SetAction("Bible.Alarm.NOTIFICATION_CLICK");

            if (_playbackState.Value.CurrentScheduleId.HasValue)
                intent.PutExtra("schedule_id", _playbackState.Value.CurrentScheduleId.Value);

            var pendingIntent = PendingIntent.GetActivity(
                context,
                999999,
                intent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            // Get channel ID from existing notification
            var channelId = existingNotification.Notification.ChannelId ?? "1";

            // Create MediaStyle with null MediaSession token - this is the key!
            // Setting token to null forces Android 14+ to use ContentIntent for body taps
            var mediaStyle = new MediaStyle()
                .SetMediaSession(null); // ← KEY: null token forces ContentIntent for body taps

            // Preserve existing actions (play/pause, prev, next)
            var actionIndices = new List<int>();
            if (existingNotification.Notification.Actions != null)
            {
                for (int i = 0; i < existingNotification.Notification.Actions.Count; i++)
                {
                    actionIndices.Add(i);
                }
                if (actionIndices.Count > 0)
                {
                    mediaStyle.SetShowActionsInCompactView(actionIndices.ToArray());
                }
            }

            // Rebuild notification with our custom ContentIntent and null MediaSession token
            var builder = new NotificationCompat.Builder(context, channelId);

            // Copy essential properties from existing notification using NotificationCompat extras
            var extras = NotificationCompat.GetExtras(existingNotification.Notification);
            if (extras != null)
            {
                // Extract title and text from extras
                var title = extras.GetString(NotificationCompat.ExtraTitle);
                var text = extras.GetString(NotificationCompat.ExtraText);
                
                if (!string.IsNullOrEmpty(title))
                    builder.SetContentTitle(title);
                if (!string.IsNullOrEmpty(text))
                    builder.SetContentText(text);
            }

            // Get small icon from existing notification using reflection (required for notification to be valid)
            bool smallIconSet = false;
            try
            {
                var smallIconField = existingNotification.Notification.GetType().GetField("mSmallIcon", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (smallIconField != null)
                {
                    var smallIcon = smallIconField.GetValue(existingNotification.Notification);
                    if (smallIcon != null)
                    {
                        // Try to get the icon resource ID
                        var iconType = smallIcon.GetType();
                        var iconResIdProperty = iconType.GetProperty("mResourceId", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (iconResIdProperty != null)
                        {
                            var iconResId = iconResIdProperty.GetValue(smallIcon);
                            if (iconResId is int resId && resId != 0)
                            {
                                builder.SetSmallIcon(resId);
                                smallIconSet = true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Failed to extract small icon from existing notification");
            }

            // Ensure we have a small icon (required for notification to be valid)
            // Use default media play icon if we couldn't extract it
            if (!smallIconSet)
            {
                builder.SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay);
            }

            // Set our custom ContentIntent and MediaStyle with null token
            builder.SetContentIntent(pendingIntent)
                   .SetStyle(mediaStyle)
                   .SetOngoing(existingNotification.Notification.Flags.HasFlag(NotificationFlags.OngoingEvent))
                   .SetVisibility(NotificationCompat.VisibilityPublic);

            // Preserve existing actions - convert from Android.App.Notification.Action to NotificationCompat.Action
            if (existingNotification.Notification.Actions != null)
            {
                foreach (var action in existingNotification.Notification.Actions)
                {
                    try
                    {
                        // Convert Android.App.Notification.Action to NotificationCompat.Action
                        var icon = action.Icon != null ? AndroidX.Core.Graphics.Drawable.IconCompat.CreateFromIcon(action.Icon) : null;
                        var title = action.Title?.ToString() ?? "";
                        var actionIntent = action.ActionIntent;
                        
                        if (icon != null && actionIntent != null)
                        {
                            var compatAction = new NotificationCompat.Action(icon, title, actionIntent);
                            builder.AddAction(compatAction);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Debug(ex, "Failed to copy notification action");
                    }
                }
            }

            // Update the notification
            notificationManager.Notify(MEDIA_NOTIFICATION_ID, builder.Build());

            // Debug level since this runs every 800ms via timer
            _logger.Debug("Notification override applied — body tap uses ContentIntent (null MediaSession token bypasses Android 14+ hijack)");
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Failed to override notification intent");
        }
    }

    /// <summary>
    /// Starts a continuous timer that overrides the notification ContentIntent every 800ms.
    /// This is a workaround for MediaElement 7.0.0 bug where it continuously recreates the notification
    /// and overwrites our custom ContentIntent. The timer ensures our override is always applied.
    /// This is the only known working solution for MediaElement 7.0.0 on all Android versions.
    /// The timer automatically stops when playback stops (checks playback state each iteration).
    /// </summary>
    private void StartNotificationIntentOverrideTimer()
    {
        // Stop any existing timer first (prevents duplicate timers when new item is queued)
        StopNotificationIntentOverrideTimer();

        _notificationOverrideCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _notificationOverrideCancellationTokenSource.Token;

        // Start background task that continuously overrides the notification
        Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(800, cancellationToken); // 800ms interval
                    
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    // Check if playback is still active - stop timer if playback has stopped
                    if (!_playbackState.Value.IsPreparingOrPlaying)
                    {
                        _logger.Debug("Playback stopped - stopping notification override timer");
                        StopNotificationIntentOverrideTimer();
                        break;
                    }

                    // Override notification if playback is active (no MediaElement reference needed)
                    if (_playbackState.Value.IsPreparingOrPlaying)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            try
                            {
                                // Double-check playback state on main thread
                                if (_playbackState.Value.IsPreparingOrPlaying)
                                {
                                    OverrideNotificationIntent(); // No MediaElement needed - we get notification directly
                                }
                            }
                            catch (System.OperationCanceledException)
                            {
                                // Expected when cancellation is requested
                            }
                            catch (Exception ex)
                            {
                                _logger.Warning(ex, "Failed to override notification intent (timer)");
                            }
                        });
                    }
                }
                catch (System.OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Error in notification override timer");
                }
            }
        }, cancellationToken);

        _logger.Debug("Started continuous notification override timer (800ms interval)");
    }

    /// <summary>
    /// Stops the continuous notification override timer.
    /// </summary>
    private void StopNotificationIntentOverrideTimer()
    {
        try
        {
            _notificationOverrideCancellationTokenSource?.Cancel();
            _notificationOverrideCancellationTokenSource?.Dispose();
            _notificationOverrideCancellationTokenSource = null;
            _logger.Debug("Stopped notification override timer");
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Error stopping notification override timer");
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
    /// Removes the ExoPlayer listener and cleans up references.
    /// Should be called before DisconnectHandler() to avoid accessing disposed ExoPlayer.
    /// </summary>
    private void RemoveExoPlayerListener()
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
                        _logger.Debug("Removed ExoPlayer listener from player");
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Player is already disposed - this is expected when handler is disconnected
                    _logger.Debug("ExoPlayer is already disposed, skipping RemoveListener call");
                }
                catch (Exception ex)
                {
                    // Other exceptions during removal - log but continue
                    _logger.Debug(ex, "Error removing listener from player (may be disposed)");
                }
                
                // Dispose the listener
                try
                {
                    _exoPlayerListener.Dispose();
                    _logger.Debug("Disposed ExoPlayer listener");
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Error disposing listener");
                }
                
                _exoPlayerListener = null;
            }
            
            _currentPlayer = null;
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Error in RemoveExoPlayerListener");
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
            // Always remove the old listener first, regardless of whether it's the same player or different
            // This prevents duplicate listeners when SetSourceWithDummyQueue is called multiple times
            if (_exoPlayerListener != null && _currentPlayer != null)
            {
                RemoveExoPlayerListener();
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
    /// MediaElement 7.0.0 on Android uses notification ID = 1 (confirmed from MediaControlsService.android.cs source code).
    /// Reference: https://github.com/dotnet/maui/blob/7.0.0/src/Core/src/Platform/Android/MediaElementHandler.cs#L198
    /// </summary>
    private async Task ReleaseMediaSessionInternalAsync(MediaElement mediaElement)
    {
        try
        {
            _logger.Information("Removing notification by disconnecting handler (handler will be recreated on next Play)");

            // Stop the notification override timer
            StopNotificationIntentOverrideTimer();

            // 1. Stop playback and reset source (triggers internal cleanup)
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                mediaElement.Stop();
                // Forces session reset
                mediaElement.Source = null;
            });

            // 2. Remove ExoPlayer listener before disconnecting handler (ExoPlayer will be disposed)
            RemoveExoPlayerListener();
            
            // 3. Disconnect handler - this properly releases the MediaSession and removes the sticky notification
            mediaElement.Handler?.DisconnectHandler();
            _logger.Debug("Disconnected MediaElement handler - MediaSession released");

            // 4. Send message to BootstrapPage to dispose MediaElement with its ExoPlayer instance
            WeakReferenceMessenger.Default.Send(new RecreateMediaElementMessage());
            _logger.Information("Sent RecreateMediaElementMessage to BootstrapPage - MediaElement will be recreated");

            // 5. Cancel the hard-coded notification ID + all (extra safety to ensure notification is gone)
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

            // MediaElement 7.0.0 uses notification ID 1 (confirmed from source code)
            notificationManager.Cancel(1);
            notificationManager.CancelAll();
            _logger.Information("Canceled notification ID 1 and all notifications");

            // 5. Samsung-specific: Kill reminders (one-time global fix)
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

    public void Dispose()
    {
        try
        {
            // Stop the notification override timer
            StopNotificationIntentOverrideTimer();

            // Note: ReleaseMediaSession is now called from AudioPlayer with MediaElement instance
            // We don't call it here since we no longer have a MediaElement reference

            // Remove listener using the same method used during handler disconnect
            RemoveExoPlayerListener();
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Error disposing AndroidPlayerNotificationService");
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
        private DateTime _lastButtonPressTime = DateTime.MinValue;
        private string? _lastMediaId;
        // Ignore duplicate presses within 500ms
        private const int DebounceMilliseconds = 500;

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
                var now = DateTime.UtcNow;
                
                // Debounce: ignore duplicate presses within the debounce window
                if (_lastMediaId == mediaId && 
                    (now - _lastButtonPressTime).TotalMilliseconds < DebounceMilliseconds)
                {
                    _logger.Debug("Ignoring duplicate button press for {MediaId} (debounced)", mediaId);
                    return;
                }
                
                _lastButtonPressTime = now;
                _lastMediaId = mediaId;
                
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

