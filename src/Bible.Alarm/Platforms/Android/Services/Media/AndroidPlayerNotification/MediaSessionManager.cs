#nullable enable
using CommunityToolkit.Maui.Core.Handlers;
using CommunityToolkit.Maui.Core.Views;
using CommunityToolkit.Maui.Views;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Media.AndroidPlayerNotification;

/// <summary>
/// Handles media session operations for Android player notifications.
/// </summary>
public sealed class MediaSessionManager(ILogger logger)
{
    /// <summary>
    /// Gets the MediaSession from MediaElement.
    /// </summary>
    public object? GetMediaSession(MediaElement mediaElement)
    {
        try
        {
            // Access MediaElement's handler
            var handler = mediaElement.Handler as MediaElementHandler;
            if (handler == null)
            {
                logger.Debug("MediaElement handler is null or not MediaElementHandler");
                return null;
            }

            // Access MediaManager property directly (now public)
            var mediaManager = handler.MediaManager;
            if (mediaManager == null)
            {
                logger.Debug("MediaManager is null");
                return null;
            }

            // Access Session property directly (now public)
            var session = mediaManager.Session;
            if (session == null)
            {
                logger.Debug("Session is null");
                return null;
            }

            return session;
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Failed to get MediaSession");
            return null;
        }
    }

    /// <summary>
    /// Releases the MediaSession and cancels the media notification.
    /// </summary>
    public async Task ReleaseMediaSessionAsync(MediaElement mediaElement)
    {
        try
        {
            logger.Information("ReleaseMediaSession called - attempting to remove notification");

            // Ensure we're on the main thread
            if (!MainThread.IsMainThread)
            {
                MainThread.BeginInvokeOnMainThread(() => ReleaseMediaSessionAsync(mediaElement).Wait());
                return;
            }

            // Add a small delay to ensure MediaElement has finished its internal stopping process
            // This helps ensure the notification is in a stable state before we try to remove it
            await Task.Run(async () =>
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
    /// Removes Android-specific notification resources.
    /// </summary>
    private async Task ReleaseMediaSessionInternalAsync()
    {
        try
        {
            logger.Information("Removing Android notification resources - MediaElement instance remains alive");

            // MediaElement is now a singleton for app lifetime - do not send DestroyMediaElementMessage
            // The MediaElement instance, ExoPlayer, and MediaSession remain alive for the entire app process
            logger.Information("MediaSession released - MediaElement instance remains alive for app lifetime");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error releasing MediaSession");
        }
    }
}
