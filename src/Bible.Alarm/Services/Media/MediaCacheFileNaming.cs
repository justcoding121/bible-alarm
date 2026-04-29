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
    /// Gets the file extension from a lookup path.
    /// LookUpPath can be a query string (e.g. ?output=json&amp;fileformat=MP4) or a CDN URL (mediator/VOD content).
    /// </summary>
    private static string GetFileExtensionFromLookUpPath(string lookUpPath)
    {
        // When LookUpPath is a CDN URL (mediator/VOD), UrlConstructionService returns track.TrackUrl.Url;
        // it has no fileformat param, so infer extension from the URL path.
        if (Uri.TryCreate(lookUpPath, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https" &&
            !string.IsNullOrEmpty(uri.AbsolutePath))
        {
            var path = uri.AbsolutePath;
            if (path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)) return ".mp4";
            if (path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) return ".mp3";
            if (path.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase)) return ".m4a";
            if (path.EndsWith(".aac", StringComparison.OrdinalIgnoreCase)) return ".aac";
        }

        // Query-string style lookup path (GETPUBMEDIALINKS refetch)
        if (lookUpPath.Contains($"{AppConstants.Media.GetPubQueryParamName.FileFormat}={AppConstants.Media.MediaStreamFormatMp4}", StringComparison.OrdinalIgnoreCase))
            return ".mp4";
        if (lookUpPath.Contains($"{AppConstants.Media.GetPubQueryParamName.FileFormat}={AppConstants.Media.MediaStreamFormatMp3}", StringComparison.OrdinalIgnoreCase))
            return ".mp3";
        if (lookUpPath.Contains($"{AppConstants.Media.GetPubQueryParamName.FileFormat}={AppConstants.Media.MediaStreamFormatM4a}", StringComparison.OrdinalIgnoreCase))
            return ".m4a";
        if (lookUpPath.Contains($"{AppConstants.Media.GetPubQueryParamName.FileFormat}={AppConstants.Media.MediaStreamFormatAac}", StringComparison.OrdinalIgnoreCase))
            return ".aac";

        return AppConstants.Media.MediaFileExtension;
    }
}

