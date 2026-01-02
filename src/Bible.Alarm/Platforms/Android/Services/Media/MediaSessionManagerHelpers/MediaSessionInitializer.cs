#nullable enable
using Android.Support.V4.Media.Session;
using Bible.Alarm.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Media.MediaSessionManagerHelpers;

/// <summary>
/// Handles MediaSession initialization and callback setup.
/// </summary>
public sealed class MediaSessionInitializer(ILogger logger, IServiceProvider serviceProvider)
{
    /// <summary>
    /// Sets the MediaSession callback on the main thread.
    /// </summary>
    public void SetMediaSessionCallback(MediaSessionCompat session)
    {
        // CRITICAL: SetCallback must be called on the main thread (requires Looper)
        // Create MediaSessionCallback lazily to avoid startup dependency resolution issues
        var playbackService = serviceProvider.GetRequiredService<IPlaybackService>();
        var serviceLogger = serviceProvider.GetRequiredService<ILogger>();
        var mediaSessionCallback = new MediaSessionCallback(playbackService, serviceLogger);

        // Ensure SetCallback runs on main thread to avoid Looper exception
        if (MainThread.IsMainThread)
        {
            // The preferred path: if we are on the main thread, execute immediately.
            session.SetCallback(mediaSessionCallback);
            logger.Debug("MediaSessionCallback set synchronously on main thread");
        }
        else
        {
            // CRITICAL: Ensure SetCallback is run on the main thread without blocking the current Binder thread.
            // The callback will be set asynchronously. The MediaSession will be operational shortly after.
            // Android Auto is tolerant of this slight delay, and blocking the Binder thread causes deadlocks/ANRs.
            logger.Warning("MediaSession creation not on MainThread. Invoking SetCallback asynchronously to avoid blocking Binder thread.");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    session?.SetCallback(mediaSessionCallback);
                    logger.Information("Successfully set MediaSessionCallback on main thread (async)");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error setting MediaSessionCallback asynchronously");
                }
            });
            // NO BLOCKING CALL HERE (e.g., .Wait() or Task.Run().Wait())
            // The Binder thread returns immediately, allowing Android Auto to complete binding without timeout.
        }
    }
}
