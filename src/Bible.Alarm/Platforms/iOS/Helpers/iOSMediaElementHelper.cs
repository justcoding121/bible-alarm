#nullable enable
using Bible.Alarm.Shared.Constants;
using CommunityToolkit.Maui;
using Serilog;

namespace Bible.Alarm.Platforms.iOS.Helpers;

/// <summary>
/// Helper class for iOS-specific MediaElement operations.
/// Handles URI processing, playback state management, and volume settings.
/// </summary>
public static class IosMediaElementHelper
{
    /// <summary>
    /// Processes a URI for iOS MediaElement.
    /// HTTPS URLs (CDN streaming) are passed through unchanged.
    /// Local file paths and file:// URIs are normalized to plain paths (iOS MediaElement works better with plain paths).
    /// </summary>
    public static string ProcessUriForMediaElement(string uri, ILogger logger)
    {
        logger.Debug(AppConstants.Logging.IosMediaElementHelperDiagnosticsLog.OriginalTrackUri, uri);

        // HTTPS URLs are CDN streaming links -- pass through unchanged
        if (uri.StartsWith(MediaUriSchemeConstants.HttpsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            logger.Debug(AppConstants.Logging.IosMediaElementHelperDiagnosticsLog.HttpsUrlPassThroughForStreaming, uri);
            return uri;
        }

        // If it's a file:// URI, convert it back to a plain path
        if (uri.StartsWith(MediaUriSchemeConstants.FilePrefix, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var fileUri = new Uri(uri);
                var localPath = fileUri.LocalPath;
                logger.Debug(AppConstants.Logging.IosMediaElementHelperDiagnosticsLog.ConvertedFileUriToPlainPath, localPath);
                return ProcessFilePath(localPath, logger);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, AppConstants.Logging.IosMediaElementHelperDiagnosticsLog.FailedToConvertFileUriUsingOriginal, uri);
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
            logger.Debug(AppConstants.Logging.IosMediaElementHelperDiagnosticsLog.NormalizedPathWithOriginal, normalizedPath, filePath);

            // Verify file exists
            if (!File.Exists(normalizedPath))
            {
                logger.Error(AppConstants.Logging.IosMediaElementHelperDiagnosticsLog.FileDoesNotExistAtNormalizedPath, normalizedPath);
                // Try the original path as fallback
                if (File.Exists(filePath))
                {
                    logger.Warning(AppConstants.Logging.IosMediaElementHelperDiagnosticsLog.FileExistsAtOriginalUsingOriginal, filePath);
                    return filePath;
                }

                logger.Error(AppConstants.Logging.IosMediaElementHelperDiagnosticsLog.FileDoesNotExistAtOriginalEither, filePath);
                throw new FileNotFoundException($"File not found: {normalizedPath}");
            }

            // Return plain file path (not file:// URI) for iOS MediaElement
            return normalizedPath;
        }
        catch (Exception ex)
        {
            // Fallback: use original path if normalization fails
            if (File.Exists(filePath))
            {
                logger.Warning(AppConstants.Logging.IosMediaElementHelperDiagnosticsLog.UsingOriginalPathAsFallback, filePath);
                return filePath;
            }

            throw new InvalidOperationException($"Error normalizing file path: {filePath}", ex);
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
        logger.Debug(AppConstants.Logging.IosMediaElementHelperDiagnosticsLog.ConfiguringIosAudioSessionBeforePlay);
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            IOsAudioSessionHelper.ConfigureAudioSession(logger, "main playback");
        });
        logger.Debug(AppConstants.Logging.IosMediaElementHelperDiagnosticsLog.IosAudioSessionConfigurationCompleted);
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
            logger.Debug(AppConstants.Logging.IosMediaElementHelperDiagnosticsLog.MediaElementStateCurrentAndSource,
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
            logger.Debug(AppConstants.Logging.IosMediaElementHelperDiagnosticsLog.MediaElementPausedSkippingStopProceedingToPlay);

            // Small delay to ensure media is fully loaded
            await Task.Delay(50);
        }
    }

    /// <summary>
    /// Sets the MediaElement source and volume for iOS.
    /// The processedUri should be either a plain file path (for cached files) or an HTTPS URL (for CDN streaming).
    /// </summary>
    public static void SetSourceAndVolume(MediaElement mediaElement, string processedUri, ILogger logger)
    {
        mediaElement.Source = processedUri;
        mediaElement.Volume = 1.0;
        logger.Debug(AppConstants.Logging.IosMediaElementHelperDiagnosticsLog.SetMediaElementSourceAndVolumeIos,
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
            logger.Debug(AppConstants.Logging.IosMediaElementHelperDiagnosticsLog.AboutToCallMediaElementPlayCurrentStateAndSource,
                mediaElement.CurrentState,
                mediaElement.Source?.ToString() ?? "null");

            mediaElement.Play();

            // Ensure volume is set to 1.0 before playing on iOS
            mediaElement.Volume = 1.0;
            logger.Debug(AppConstants.Logging.IosMediaElementHelperDiagnosticsLog.SetVolumeBeforePlayIos,
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
            logger.Debug(AppConstants.Logging.IosMediaElementHelperDiagnosticsLog.NotPlayingOrBufferingAfterPlayWaitingRetry, stateAfterPlay);
            await Task.Delay(200);

            var stateAfterWait = await GetCurrentStateAsync(mediaElement, logger);
            var waitStateString = stateAfterWait.ToString();
            var isStillPlayingOrBuffering = waitStateString is "Playing" or "Buffering";

            if (!isStillPlayingOrBuffering)
            {
                logger.Debug(AppConstants.Logging.IosMediaElementHelperDiagnosticsLog.StillNotPlayingAfterWaitAttemptPlayAgain, stateAfterWait);
                await MainThread.InvokeOnMainThreadAsync(mediaElement.Play);
                await Task.Delay(100);
                stateAfterWait = await GetCurrentStateAsync(mediaElement, logger);
                logger.Debug(AppConstants.Logging.IosMediaElementHelperDiagnosticsLog.AfterRetryMediaElementState, stateAfterWait);
            }
        }
    }
}

