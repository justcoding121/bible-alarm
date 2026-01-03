#nullable enable
using AndroidX.Media3.DataSource;
using Bible.Alarm.Services.Media.Interfaces;
using CommunityToolkit.Maui.Views;
using Serilog;
using Application = Android.App.Application;
using Exception = System.Exception;
using Uri = Android.Net.Uri;
using Bible.Alarm.Platforms.Android.Services.Media.AndroidPlayerNotificationHelpers;

namespace Bible.Alarm.Platforms.Android.Services.Media;

/// <summary>
/// Android-specific service for enabling the Next and Previous buttons in system media controls
/// by creating a multi-item queue in ExoPlayer using ConcatenatingMediaSource.
/// Uses three items: dummy previous, current, and dummy next to enable both navigation buttons.
/// </summary>
public sealed class AndroidPlayerNotificationService(ILogger logger) : IAndroidPlayerNotificationService, IDisposable
{
    // Helper classes
    private readonly MediaSourceBuilder mediaSourceBuilder = new(logger);
    private readonly PlayerManager playerManager = new(logger);
    private readonly MediaSessionManager mediaSessionManager = new(logger);
    private readonly NotificationLogger notificationLogger = new(logger);

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
            var player = playerManager.GetExoPlayer(mediaElement);
            if (player == null)
            {
                logger.Error("Failed to get ExoPlayer instance");
                return;
            }

            var dataSourceFactory = CreateDataSourceFactory();
            var androidUri = ParseUri(uri);
            if (androidUri == null)
            {
                return;
            }

            var sources = mediaSourceBuilder.BuildMediaSources(dataSourceFactory, androidUri, isFirstTrack, isLastTrack);
            if (sources == null)
            {
                return;
            }

            playerManager.ConfigurePlayerWithSources(player, sources, isFirstTrack);
            notificationLogger.LogQueueConfiguration(sources.Count, isFirstTrack, isLastTrack);
            playerManager.VerifyPlayerCapabilities(player);
            notificationLogger.LogFinalConfirmation(sources.Count);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to set queue in SetSourceWithDummyQueue");
        }
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

    /// <summary>
    /// Releases the MediaSession and cancels the media notification.
    /// The key fix: calls SetPlayer(null) on PlayerNotificationManager, which is the only way to actually dismiss the notification.
    /// </summary>
    public void ReleaseMediaSession(MediaElement mediaElement)
    {
        mediaSessionManager.ReleaseMediaSessionAsync(mediaElement).Wait();
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

            // Remove listener using PlayerManager
            playerManager.RemoveExoPlayerListener();
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Error disposing AndroidPlayerNotificationService");
        }

        // All injected services (logger) are singletons, so don't dispose them
    }

}
