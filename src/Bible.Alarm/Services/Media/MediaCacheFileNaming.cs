#nullable enable

using System.Text;
using Bible.Alarm.Shared.Constants;

namespace Bible.Alarm.Services.Media;

internal static class MediaCacheFileNaming
{
    /// <summary>
    /// Gets the cache file name from a lookup path (API query string).
    /// Uses lookup path instead of CDN URL so cache files are stable even when CDN URLs change.
    /// This enables offline playback by building lookup paths from schedule config.
    /// </summary>
    internal static string GetCacheFileName(string lookUpPath)
    {
        if (string.IsNullOrWhiteSpace(lookUpPath))
        {
            throw new ArgumentException("LookUpPath cannot be null or empty", nameof(lookUpPath));
        }

        // Hash the lookup path to create a safe filename
        var plainTextBytes = Encoding.UTF8.GetBytes(lookUpPath);
        var base64Name = Convert.ToBase64String(plainTextBytes)
            .Replace('/', '_')  // Replace / with _ for filesystem safety
            .Replace('+', '-')  // Replace + with - for filesystem safety
            .Replace("=", "");  // Remove padding

        // Determine file extension from lookup path
        var extension = GetFileExtensionFromLookUpPath(lookUpPath);

        return base64Name + extension;
    }

    /// <summary>
    /// Gets the file extension from a lookup path based on fileformat parameter or default to .mp3
    /// </summary>
    private static string GetFileExtensionFromLookUpPath(string lookUpPath)
    {
        // Check for fileformat parameter in lookup path
        if (lookUpPath.Contains("fileformat=MP4", StringComparison.OrdinalIgnoreCase))
        {
            return ".mp4";
        }

        if (lookUpPath.Contains("fileformat=MP3", StringComparison.OrdinalIgnoreCase))
        {
            return ".mp3";
        }

        if (lookUpPath.Contains("fileformat=M4A", StringComparison.OrdinalIgnoreCase))
        {
            return ".m4a";
        }

        if (lookUpPath.Contains("fileformat=AAC", StringComparison.OrdinalIgnoreCase))
        {
            return ".aac";
        }

        // Default to mp3 for backwards compatibility
        return AppConstants.Media.MediaFileExtension;
    }
}

