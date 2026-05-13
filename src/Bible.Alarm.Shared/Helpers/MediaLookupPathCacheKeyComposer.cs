#nullable enable

using System;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Combines hashed lookup-path token with inferred extension for SQLite/media cache filenames.
/// </summary>
public static class MediaLookupPathCacheKeyComposer
{
    public static string ComposeCacheFileName(string lookUpPath)
    {
        if (string.IsNullOrWhiteSpace(lookUpPath))
        {
            throw new ArgumentException("LookUpPath cannot be null or empty", nameof(lookUpPath));
        }

        return Utf8LookupPathSafeFilenameToken.FromLookupPath(lookUpPath)
               + LookupPathMediaFileExtensionResolver.Resolve(lookUpPath);
    }
}
