#nullable enable

using System;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Resolves bootstrap log directory root (packaged LocalCache vs exe-relative fallback) without WinRT references.
/// </summary>
public static class BootstrapLogRootResolver
{
    /// <summary>
    /// First tries <paramref name="tryGetPackagedLocalCacheFolderPath"/>; on failure falls back to <paramref name="tryGetExeFallbackDirectory"/>.
    /// </summary>
    public static bool TryGetLogRoot(
        out string rootDirectory,
        Func<string> tryGetPackagedLocalCacheFolderPath,
        Func<string> tryGetExeFallbackDirectory)
    {
        ArgumentNullException.ThrowIfNull(tryGetPackagedLocalCacheFolderPath);
        ArgumentNullException.ThrowIfNull(tryGetExeFallbackDirectory);

        rootDirectory = null!;
        try
        {
            rootDirectory = tryGetPackagedLocalCacheFolderPath();
            return !string.IsNullOrWhiteSpace(rootDirectory);
        }
        catch (Exception)
        {
            try
            {
                rootDirectory = tryGetExeFallbackDirectory();
                return !string.IsNullOrWhiteSpace(rootDirectory);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
