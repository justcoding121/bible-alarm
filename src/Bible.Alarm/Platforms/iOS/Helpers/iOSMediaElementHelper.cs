#nullable enable
using CommunityToolkit.Maui.Views;
using Foundation;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using MediaElementState = CommunityToolkit.Maui.Views.MediaElementState;

namespace Bible.Alarm.Platforms.iOS.Helpers;

/// <summary>
/// Helper class for iOS-specific MediaElement operations.
/// Handles URI processing, playback state management, and volume settings.
/// </summary>
public static class iOSMediaElementHelper
{
    /// <summary>
    /// Processes a URI for iOS MediaElement by normalizing paths and converting to file:// format.
    /// Note: MediaCacheService always returns cached file paths, never HTTP/HTTPS URLs.
    /// </summary>
    public static string ProcessUriForMediaElement(string uri, ILogger logger)
    {
        logger.Debug("Original track URI: {Uri}", uri);
        
        if (!uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessFilePath(uri, logger);
        }
        else
        {
            return ProcessFileUri(uri, logger);
        }
    }

    /// <summary>
    /// Processes a plain file path (not file:// URI) for iOS by normalizing and converting to file:// format.
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
                    return ConvertToFileUri(filePath, logger);
                }
                else
                {
                    logger.Error("File does not exist at original path either: {Path}", filePath);
                    throw new FileNotFoundException($"File not found: {normalizedPath}");
                }
            }
            
            return ConvertToFileUri(normalizedPath, logger);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error normalizing file path: {Path}", filePath);
            // Fallback: use original path if normalization fails
            if (File.Exists(filePath))
            {
                logger.Warning("Using original path as fallback: {Path}", filePath);
                return ConvertToFileUri(filePath, logger);
            }
            else
            {
                logger.Error("Original path also does not exist: {Path}", filePath);
                throw;
            }
        }
    }

    /// <summary>
    /// Normalizes a file path by resolving ../ and ./ segments.
    /// </summary>
    private static string NormalizeFilePath(string path)
    {
        var isAbsolute = path.StartsWith("/");
        var pathParts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
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
    /// Converts a file path to a file:// URI using NSUrl for iOS.
    /// </summary>
    private static string ConvertToFileUri(string filePath, ILogger logger)
    {
        var nsUrl = NSUrl.FromFilename(filePath);
        var uri = nsUrl.AbsoluteString ?? filePath;
        logger.Debug("Converted path to file:// URI for iOS: {Uri} (original path: {Path})", uri, filePath);
        return uri;
    }

    /// <summary>
    /// Processes a file:// URI for iOS by ensuring proper format and verifying file exists.
    /// </summary>
    private static string ProcessFileUri(string uri, ILogger logger)
    {
        // Ensure proper file:// URL format for iOS (file:/// for absolute paths)
        if (!uri.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
        {
            uri = uri.Replace("file://", "file:///");
            logger.Debug("Formatted file URI for iOS: {Uri}", uri);
        }
        
        // Verify file exists by converting to local path
        try
        {
            var fileUri = new Uri(uri);
            var localPath = fileUri.LocalPath;
            if (!File.Exists(localPath))
            {
                logger.Error("File does not exist at path: {Path}", localPath);
                throw new FileNotFoundException($"File not found: {localPath}");
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error verifying file:// URI: {Uri}", uri);
            throw;
        }
        
        return uri;
    }

    /// <summary>
    /// Configures iOS audio session before playing. Critical for MediaElement to actually play audio on iOS.
    /// </summary>
    public static async Task ConfigureAudioSessionBeforePlayAsync(ILogger logger)
    {
        logger.Debug("Configuring iOS audio session before Play()");
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            iOSAudioSessionHelper.ConfigureAudioSession(logger, "main playback");
        });
        logger.Debug("iOS audio session configuration completed");
    }

    /// <summary>
    /// Gets the current MediaElement state on the main thread.
    /// </summary>
    public static async Task<MediaElementState> GetCurrentStateAsync(MediaElement mediaElement, ILogger logger)
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
    /// Handles the Paused state on iOS by stopping first, as calling Play() directly may not work.
    /// </summary>
    public static async Task HandlePausedStateAsync(MediaElement mediaElement, MediaElementState currentState, ILogger logger)
    {
        if (currentState == MediaElementState.Paused)
        {
            logger.Debug("MediaElement is in Paused state on iOS, stopping first then playing");
            await MainThread.InvokeOnMainThreadAsync(() => mediaElement.Stop());
            await Task.Delay(50);
            
            var stateAfterStop = await GetCurrentStateAsync(mediaElement, logger);
            logger.Debug("MediaElement state after Stop(): {State}", stateAfterStop);
        }
    }

    /// <summary>
    /// Sets the MediaElement source and volume for iOS.
    /// </summary>
    public static void SetSourceAndVolume(MediaElement mediaElement, string processedUri, ILogger logger)
    {
        mediaElement.Source = processedUri;
        mediaElement.Volume = 1.0;
        logger.Debug("Set MediaElement Volume to 1.0 on iOS. Source: {Source}, CurrentState: {State}", 
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
    public static async Task RetryPlayIfNeededAsync(MediaElement mediaElement, MediaElementState stateAfterPlay, ILogger logger)
    {
        if (stateAfterPlay != MediaElementState.Playing && stateAfterPlay != MediaElementState.Buffering)
        {
            logger.Debug("MediaElement not in Playing/Buffering state after Play(), waiting longer and retrying. Current state: {State}", stateAfterPlay);
            await Task.Delay(200);
            
            var stateAfterWait = await GetCurrentStateAsync(mediaElement, logger);
            if (stateAfterWait != MediaElementState.Playing && stateAfterWait != MediaElementState.Buffering)
            {
                logger.Debug("MediaElement still not playing after wait, attempting Play() again. State: {State}", stateAfterWait);
                await MainThread.InvokeOnMainThreadAsync(() => mediaElement.Play());
                await Task.Delay(100);
                stateAfterWait = await GetCurrentStateAsync(mediaElement, logger);
                logger.Debug("After retry, MediaElement state: {State}", stateAfterWait);
            }
        }
    }
}

