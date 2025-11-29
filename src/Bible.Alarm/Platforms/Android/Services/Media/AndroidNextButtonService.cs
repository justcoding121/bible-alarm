#nullable enable
using Android.Net;
using Android.App;
using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.ExoPlayer.Source;
using AndroidX.Media3.DataSource;
using Bible.Alarm.Services.Media.Interfaces;
using CommunityToolkit.Maui.Views;
using Serilog;
using System.Reflection;

namespace Bible.Alarm.Platforms.Android.Services.Media;

/// <summary>
/// Android-specific service for enabling the Next button in system media controls
/// by creating a multi-item queue in ExoPlayer using ConcatenatingMediaSource.
/// </summary>
public class AndroidNextButtonService : IAndroidNextButtonService
{
    private readonly MediaElement _mediaElement;
    private readonly ILogger _logger;

    public AndroidNextButtonService(MediaElement mediaElement, ILogger logger)
    {
        _mediaElement = mediaElement;
        _logger = logger;
    }

    /// <summary>
    /// Sets a multi-item queue via ExoPlayer using ConcatenatingMediaSource to enable the Next button.
    /// Uses two distinct MediaItems (with different MediaIds and URI fragments) pointing to the same file.
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
            var context = Application.Context;
            var dataSourceFactory = new DefaultDataSourceFactory(context);

            // Parse the URI
            var androidUri = Uri.Parse(uri);

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
                .CreateMediaSource(currentItem);
            var source2 = new ProgressiveMediaSource.Factory(dataSourceFactory)
                .CreateMediaSource(dummyNextItem);

            // Create ConcatenatingMediaSource with both sources
            var concatenatingSource = new ConcatenatingMediaSource(source1, source2);

            // Set the concatenating source on the player
            player.SetMediaSource(concatenatingSource);
            
            // CRITICAL: Call Prepare() AFTER setting the source to trigger TimelineChanged event
            // This is what makes MediaSessionConnector see HasNextMediaItem = true
            player.Prepare();
            
            // Do NOT call player.Play() here - let the normal Play() flow handle it

            _logger.Information("Set ConcatenatingMediaSource queue in 7.0.0 — Next button will appear.");
            
            // Verify HasNextMediaItem is true
            if (player.HasNextMediaItem)
            {
                _logger.Debug("Player HasNextMediaItem: {HasNext}", player.HasNextMediaItem);
            }
            else
            {
                _logger.Debug("Player HasNextMediaItem is still false after setting ConcatenatingMediaSource");
            }

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
}

