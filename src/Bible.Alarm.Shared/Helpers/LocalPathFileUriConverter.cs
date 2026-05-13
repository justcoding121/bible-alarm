#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Converts local filesystem paths to file:// URIs for WinRT/XML contexts without touching MAUI handlers.
/// </summary>
public static class LocalPathFileUriConverter
{
    public static bool TryCreateUriString(string path, [NotNullWhen(true)] out string? uriString)
    {
        ArgumentNullException.ThrowIfNull(path);

        try
        {
            var absolutePath = Path.GetFullPath(path);
            uriString = new Uri(absolutePath).ToString();
            return true;
        }
        catch (Exception)
        {
            uriString = null;
            return false;
        }
    }
}
