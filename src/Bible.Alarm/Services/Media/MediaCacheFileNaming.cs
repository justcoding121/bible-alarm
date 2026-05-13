#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Services.Media;

internal static class MediaCacheFileNaming
{
    /// <summary>
    /// Gets the cache file name from a lookup path (API query string).
    /// Uses lookup path instead of CDN URL so cache files are stable even when CDN URLs change.
    /// This enables offline playback by building lookup paths from schedule config.
    /// </summary>
    internal static string GetCacheFileName(string lookUpPath) =>
        MediaLookupPathCacheKeyComposer.ComposeCacheFileName(lookUpPath);
}

