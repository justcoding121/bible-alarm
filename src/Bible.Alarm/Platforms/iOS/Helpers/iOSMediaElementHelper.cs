#nullable enable
using CommunityToolkit.Maui.Views;
using Serilog;

namespace Bible.Alarm.Platforms.iOS.Helpers;

/// <summary>
/// Helper class for iOS-specific MediaElement operations.
/// Handles URI processing, playback state management, and volume settings.
/// </summary>
public static class IosMediaElementHelper
{
    /// <summary>
    /// Processes a URI for iOS MediaElement by normalizing paths.
    /// On iOS, MediaElement works better with plain file paths instead of file:// URIs.
    /// Note: MediaCacheService always returns cached file paths, never HTTP/HTTPS URLs.
    /// </summary>
    public static string ProcessUriForMediaElement(string uri, ILogger logger)
    {
        logger.Debug("Original track URI: {Uri}", uri);

        // If it's already a file:// URI, convert it back to a plain path
        if (uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var fileUri = new Uri(uri);
                var localPath = fileUri.LocalPath;
                logger.Debug("Converted file:// URI to plain path: {Path}", localPath);
                return ProcessFilePath(localPath, logger);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Failed to convert file:// URI, using original: {Uri}", uri);
                return ProcessFilePath(uri, logger);
            }
        }

        return ProcessFilePath(uri, logger);
    }

    /// <summary>
    /// Processes a plain file path for iOS by normalizing it.
    /// Returns the normalized plain file path (not a file:// URI) as MediaElement on iOS works better with plain paths.
    /// </summary>
    private static string ProcessFilePath(string filePath, ILogger logger)
    {
        try
        {
            var normalizedPath = NormalizeFilePath(filePath);
            logger.Debug("Normalized path: {Path} (original: {Original})", normalizedPath, filePath);

            // Verify file exists
            if (!File.Exists(normalizedPath))
            {
                logger.Error("File does not exist at normalized path: {Path}", normalizedPath);
                // Try the original path as fallback
                if (File.Exists(filePath))
                {
                    logger.Warning("File exists at original path, using original: {Path}", filePath);
                    return filePath;
                }

                logger.Error("File does not exist at original path either: {Path}", filePath);
                throw new FileNotFoundException($"File not found: {normalizedPath}");
            }

            // Return plain file path (not file:// URI) for iOS MediaElement
            return normalizedPath;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error normalizing file path: {Path}", filePath);
            // Fallback: use original path if normalization fails
            if (File.Exists(filePath))
            {
                logger.Warning("Using original path as fallback: {Path}", filePath);
                return filePath;
            }

            logger.Error("Original path also does not exist: {Path}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Normalizes a file path by resolving ../ and ./ segments.
    /// </summary>
    private static string NormalizeFilePath(string path)
    {
        var isAbsolute = path.StartsWith("/");
        var pathParts = path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        var normalizedParts = new List<string>();

        foreach (var part in pathParts)
        {
            if (part == "..")
            {
                if (normalizedParts.Count > 0)
                {
                    normalizedParts.RemoveAt(normalizedParts.Count - 1);
                }
            }
            else if (part != "." && !string.IsNullOrEmpty(part))
            {
                normalizedParts.Add(part);
            }
        }

        return isAbsolute ? "/" + string.Join("/", normalizedParts) : string.Join("/", normalizedParts);
    }

    /// <summary>
    /// Configures iOS audio session before playing. Critical for MediaElement to actually play audio on iOS.
    /// </summary>
    public static async Task ConfigureAudioSessionBeforePlayAsync(ILogger logger)
    {
        logger.Debug("Configuring iOS audio session before Play()");
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            IOsAudioSessionHelper.ConfigureAudioSession(logger, "main playback");
        });
        logger.Debug("iOS audio session configuration completed");
    }

    /// <summary>
    /// Gets the current MediaElement state on the main thread.
    /// Returns the state as object since MediaElementState enum type may not be accessible in this context.
    /// </summary>
    public static async Task<object> GetCurrentStateAsync(MediaElement mediaElement, ILogger logger)
    {
        return await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var state = mediaElement.CurrentState;
            logger.Debug("MediaElement state: {CurrentState}, Source: {Source}",
                state,
                mediaElement.Source?.ToString() ?? "null");
            return state;
        });
    }

    /// <summary>
    /// Handles the Paused state on iOS.
    /// When MediaElement is in Paused state right after setting a new source, we can just call Play() directly.
    /// Only call Stop() if the player was previously playing and then paused (which is rare in our use case).
    /// </summary>
    public static async Task HandlePausedStateAsync(MediaElement mediaElement, object currentState, ILogger logger)
    {
        // Check if state is Paused by comparing string representation
        var stateString = currentState.ToString();
        if (stateString == "Paused")
        {
            // When a new source is set, iOS MediaElement transitions to "Paused" state (loaded but not playing).
            // In this case, we don't need to Stop() first - we can just Play() directly.
            // Stop() tries to seek to zero, which can fail if the player isn't ready yet (seekable ranges not available).
            // Only try Stop() if we're resuming from a previously paused playback, but that's rare in our alarm flow.
            // For now, skip Stop() and just proceed with Play() - it should work fine for newly loaded media.
            logger.Debug("MediaElement is in Paused state on iOS. Skipping Stop() and proceeding directly to Play() - this is safe for newly loaded media");
            
            // Small delay to ensure media is fully loaded
            await Task.Delay(50);
        }
    }

    /// <summary>
    /// Sets the MediaElement source and volume for iOS.
    /// The processedUri should already be a plain file path (not a file:// URI).
    /// </summary>
    public static void SetSourceAndVolume(MediaElement mediaElement, string processedUri, ILogger logger)
    {
        mediaElement.Source = processedUri;
        mediaElement.Volume = 1.0;
        logger.Debug("Set MediaElement Source and Volume on iOS. Source: {Source}, CurrentState: {State}",
            mediaElement.Source?.ToString() ?? "null",
            mediaElement.CurrentState);
    }

    /// <summary>
    /// Invokes Play() on the main thread and sets volume on iOS.
    /// </summary>
    public static async Task InvokePlayAsync(MediaElement mediaElement, ILogger logger)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            logger.Debug("About to call MediaElement.Play(). Current state: {State}, Source: {Source}",
                mediaElement.CurrentState,
                mediaElement.Source?.ToString() ?? "null");

            mediaElement.Play();

            // Ensure volume is set to 1.0 before playing on iOS
            mediaElement.Volume = 1.0;
            logger.Debug("Set MediaElement Volume to 1.0 before Play() on iOS. Current Volume: {Volume}, State after Play(): {State}",
                mediaElement.Volume,
                mediaElement.CurrentState);
        });
    }

    /// <summary>
    /// Retries Play() on iOS if playback didn't start after initial attempt.
    /// </summary>
    public static async Task RetryPlayIfNeededAsync(MediaElement mediaElement, object stateAfterPlay, ILogger logger)
    {
        // Check if state is Playing or Buffering by comparing string representation
        var stateString = stateAfterPlay.ToString();
        var isPlayingOrBuffering = stateString is "Playing" or "Buffering";

        if (!isPlayingOrBuffering)
        {
            logger.Debug("MediaElement not in Playing/Buffering state after Play(), waiting longer and retrying. Current state: {State}", stateAfterPlay);
            await Task.Delay(200);

            var stateAfterWait = await GetCurrentStateAsync(mediaElement, logger);
            var waitStateString = stateAfterWait.ToString();
            var isStillPlayingOrBuffering = waitStateString is "Playing" or "Buffering";

            if (!isStillPlayingOrBuffering)
            {
                logger.Debug("MediaElement still not playing after wait, attempting Play() again. State: {State}", stateAfterWait);
                await MainThread.InvokeOnMainThreadAsync(mediaElement.Play);
                await Task.Delay(100);
                stateAfterWait = await GetCurrentStateAsync(mediaElement, logger);
                logger.Debug("After retry, MediaElement state: {State}", stateAfterWait);
            }
        }
    }
}

