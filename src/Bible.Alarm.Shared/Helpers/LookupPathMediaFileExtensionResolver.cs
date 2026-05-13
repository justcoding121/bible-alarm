#nullable enable

using System;
using Bible.Alarm.Shared.Constants;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Chooses media filename extension from lookup paths that may be absolute CDN URIs or GETPUBMEDIALINKS-style query fragments.
/// </summary>
public static class LookupPathMediaFileExtensionResolver
{
    public static string Resolve(string lookUpPath)
    {
        ArgumentNullException.ThrowIfNull(lookUpPath);

        if (Uri.TryCreate(lookUpPath, UriKind.Absolute, out var uri) &&
            (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) &&
            !string.IsNullOrEmpty(uri.AbsolutePath))
        {
            var path = uri.AbsolutePath;
            if (path.EndsWith(AppConstants.Media.MediaVideoFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                return AppConstants.Media.MediaVideoFileExtension;
            }

            if (path.EndsWith(AppConstants.Media.MediaFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                return AppConstants.Media.MediaFileExtension;
            }

            if (path.EndsWith(AppConstants.Media.MediaM4aFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                return AppConstants.Media.MediaM4aFileExtension;
            }

            if (path.EndsWith(AppConstants.Media.MediaAacFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                return AppConstants.Media.MediaAacFileExtension;
            }
        }

        if (lookUpPath.Contains($"{AppConstants.Media.GetPubQueryParamName.FileFormat}={AppConstants.Media.MediaStreamFormatMp4}", StringComparison.OrdinalIgnoreCase))
        {
            return AppConstants.Media.MediaVideoFileExtension;
        }

        if (lookUpPath.Contains($"{AppConstants.Media.GetPubQueryParamName.FileFormat}={AppConstants.Media.MediaStreamFormatMp3}", StringComparison.OrdinalIgnoreCase))
        {
            return AppConstants.Media.MediaFileExtension;
        }

        if (lookUpPath.Contains($"{AppConstants.Media.GetPubQueryParamName.FileFormat}={AppConstants.Media.MediaStreamFormatM4a}", StringComparison.OrdinalIgnoreCase))
        {
            return AppConstants.Media.MediaM4aFileExtension;
        }

        if (lookUpPath.Contains($"{AppConstants.Media.GetPubQueryParamName.FileFormat}={AppConstants.Media.MediaStreamFormatAac}", StringComparison.OrdinalIgnoreCase))
        {
            return AppConstants.Media.MediaAacFileExtension;
        }

        return AppConstants.Media.MediaFileExtension;
    }
}
