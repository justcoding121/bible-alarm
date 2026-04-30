#nullable enable

using System;
using Microsoft.Data.Sqlite;

namespace Bible.Alarm.Shared.Services.Media.Helpers.SectionFetcherHelpers;

/// <summary>
/// Shared SQLite exception classification for section-fetch retry / constraint handling.
/// </summary>
public static class SectionFetcherSqliteExceptionHelper
{
    public static bool IsBusyOrLocked(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is SqliteException sqliteEx)
            {
                var code = (int)sqliteEx.SqliteErrorCode;
                if (code is 5 or 6)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool IsUniqueConstraintViolation(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is SqliteException sqliteEx && (int)sqliteEx.SqliteErrorCode == 19)
            {
                return true;
            }
        }

        return false;
    }
}
