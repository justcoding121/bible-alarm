#nullable enable

using System;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Resolves artwork URLs for adaptive toast XML: keeps http(s) URIs and maps local paths to file:// URIs.
/// </summary>
public static class ToastArtworkImageSrcResolver
{
    public static string? TryResolve(string? artworkUrl)
    {
        if (string.IsNullOrWhiteSpace(artworkUrl))
        {
            return null;
        }

        var trimmed = artworkUrl.Trim();

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
            (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            return trimmed;
        }

        return LocalPathFileUriConverter.TryCreateUriString(trimmed, out var fileUri) ? fileUri : null;
    }
}
