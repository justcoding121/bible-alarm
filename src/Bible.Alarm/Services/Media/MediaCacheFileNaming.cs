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
            .Replace("=", string.Empty, StringComparison.Ordinal);  // Remove padding

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
        // CDN lookup paths: UrlConstructionService may set track TrackUrl URL with no fileformat param — infer extension from path.
        if (Uri.TryCreate(lookUpPath, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https" &&
            !string.IsNullOrEmpty(uri.AbsolutePath))
        {
            var path = uri.AbsolutePath;
            if (path.EndsWith(AppConstants.Media.MediaVideoFileExtension, StringComparison.OrdinalIgnoreCase)) return AppConstants.Media.MediaVideoFileExtension;
            if (path.EndsWith(AppConstants.Media.MediaFileExtension, StringComparison.OrdinalIgnoreCase)) return AppConstants.Media.MediaFileExtension;
            if (path.EndsWith(AppConstants.Media.MediaM4aFileExtension, StringComparison.OrdinalIgnoreCase)) return AppConstants.Media.MediaM4aFileExtension;
            if (path.EndsWith(AppConstants.Media.MediaAacFileExtension, StringComparison.OrdinalIgnoreCase)) return AppConstants.Media.MediaAacFileExtension;
        }

        // Query-string style lookup path (GETPUBMEDIALINKS refetch)
        if (lookUpPath.Contains($"{AppConstants.Media.GetPubQueryParamName.FileFormat}={AppConstants.Media.MediaStreamFormatMp4}", StringComparison.OrdinalIgnoreCase))
            return AppConstants.Media.MediaVideoFileExtension;
        if (lookUpPath.Contains($"{AppConstants.Media.GetPubQueryParamName.FileFormat}={AppConstants.Media.MediaStreamFormatMp3}", StringComparison.OrdinalIgnoreCase))
            return AppConstants.Media.MediaFileExtension;
        if (lookUpPath.Contains($"{AppConstants.Media.GetPubQueryParamName.FileFormat}={AppConstants.Media.MediaStreamFormatM4a}", StringComparison.OrdinalIgnoreCase))
            return AppConstants.Media.MediaM4aFileExtension;
        if (lookUpPath.Contains($"{AppConstants.Media.GetPubQueryParamName.FileFormat}={AppConstants.Media.MediaStreamFormatAac}", StringComparison.OrdinalIgnoreCase))
            return AppConstants.Media.MediaAacFileExtension;

        return AppConstants.Media.MediaFileExtension;
    }
}

