#nullable enable

using System.IO;
using Microsoft.Data.Sqlite;

namespace Bible.Alarm.Services.Media.MediaIndexServiceHelpers;

/// <summary>
/// Attaches the old media index database using a bound parameter so the path is not concatenated into SQL (S2077).
/// </summary>
internal static class OldMediaIndexSqliteAttachHelper
{
    internal const string OldMediaAlias = "old_media";

    private const string OldMediaPathParameter = "@old_media_path";

    internal static async Task AttachOldMediaDatabaseAsync(SqliteConnection connection, string oldMediaIndexDbPath)
    {
        var resolved = Path.GetFullPath(oldMediaIndexDbPath);
        if (!File.Exists(resolved))
        {
            throw new FileNotFoundException("Old media index database not found.", resolved);
        }

        if (resolved.AsSpan().IndexOf('\0') >= 0)
        {
            throw new ArgumentException("Path contains invalid characters.", nameof(oldMediaIndexDbPath));
        }

        using var attachCmd = connection.CreateCommand();
        attachCmd.CommandText = $"ATTACH DATABASE {OldMediaPathParameter} AS {OldMediaAlias}";
        attachCmd.Parameters.AddWithValue(OldMediaPathParameter, resolved);
        await attachCmd.ExecuteNonQueryAsync();
    }
}
