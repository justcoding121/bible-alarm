#nullable enable

using System;
using System.Text;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Builds a filesystem-safe token from UTF-8 lookup path bytes (stable cache filenames).
/// </summary>
public static class Utf8LookupPathSafeFilenameToken
{
    public static string FromLookupPath(string lookUpPath)
    {
        ArgumentNullException.ThrowIfNull(lookUpPath);

        var plainTextBytes = Encoding.UTF8.GetBytes(lookUpPath);
        return Convert.ToBase64String(plainTextBytes)
            .Replace('/', '_')
            .Replace('+', '-')
            .Replace("=", string.Empty, StringComparison.Ordinal);
    }
}
