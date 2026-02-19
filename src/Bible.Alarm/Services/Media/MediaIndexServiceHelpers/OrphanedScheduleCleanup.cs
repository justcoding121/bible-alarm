#nullable enable

using Bible.Alarm.Shared.Constants;
using Microsoft.Data.Sqlite;
using Serilog;

namespace Bible.Alarm.Services.Media.MediaIndexServiceHelpers;

/// <summary>
/// After media index replacement, verifies that all non-English schedule references
/// can be resolved in the new media index. Deletes schedules whose main publication
/// can't be found, and resets music for schedules whose music publication can't be found.
/// </summary>
internal sealed class OrphanedScheduleCleanup(ILogger logger)
{
    private record ScheduleRef(int AlarmScheduleId, string PublicationCode, string LanguageCode);

    public async Task CleanupAsync(string mediaIndexDbPath, string scheduleDbPath)
    {
        if (!File.Exists(scheduleDbPath) || !File.Exists(mediaIndexDbPath))
        {
            return;
        }

        var bibleRefs = await ReadRefsAsync(scheduleDbPath,
            """
            SELECT AlarmScheduleId, PublicationCode, LanguageCode
            FROM BiblePublicationSchedules
            WHERE LanguageCode IS NOT NULL AND LanguageCode != @defaultLang
            """);

        var musicRefs = await ReadRefsAsync(scheduleDbPath,
            """
            SELECT AlarmScheduleId, PublicationCode, LanguageCode
            FROM AlarmMusic
            WHERE LanguageCode IS NOT NULL AND LanguageCode != @defaultLang
            """);

        if (bibleRefs.Count == 0 && musicRefs.Count == 0)
        {
            return;
        }

        var scheduleIdsToDelete = new List<int>();
        var musicScheduleIdsToReset = new List<int>();

        using (var mediaConn = new SqliteConnection($"Data Source={mediaIndexDbPath};Mode=ReadOnly"))
        {
            await mediaConn.OpenAsync();

            foreach (var r in bibleRefs)
            {
                if (!await PublicationExistsAsync(mediaConn, r.PublicationCode, r.LanguageCode))
                {
                    scheduleIdsToDelete.Add(r.AlarmScheduleId);
                    logger.Warning(
                        "Publication {PubCode}/{LangCode} not in new media index; deleting schedule {ScheduleId}",
                        r.PublicationCode, r.LanguageCode, r.AlarmScheduleId);
                }
            }

            foreach (var r in musicRefs)
            {
                if (scheduleIdsToDelete.Contains(r.AlarmScheduleId))
                {
                    continue;
                }

                if (!await PublicationExistsAsync(mediaConn, r.PublicationCode, r.LanguageCode))
                {
                    musicScheduleIdsToReset.Add(r.AlarmScheduleId);
                    logger.Warning(
                        "Music pub {PubCode}/{LangCode} not in new media index; resetting music for schedule {ScheduleId}",
                        r.PublicationCode, r.LanguageCode, r.AlarmScheduleId);
                }
            }
        }

        if (scheduleIdsToDelete.Count == 0 && musicScheduleIdsToReset.Count == 0)
        {
            logger.Information("All non-English schedule references verified in new media index");
            return;
        }

        using var scheduleConn = new SqliteConnection($"Data Source={scheduleDbPath}");
        await scheduleConn.OpenAsync();

        foreach (var scheduleId in scheduleIdsToDelete)
        {
            await DeleteAlarmScheduleAsync(scheduleConn, scheduleId);
        }

        foreach (var scheduleId in musicScheduleIdsToReset)
        {
            await ResetAlarmMusicAsync(scheduleConn, scheduleId);
        }

        logger.Information(
            "Cleaned up {DeletedCount} orphaned schedule(s) and reset music for {ResetCount} schedule(s)",
            scheduleIdsToDelete.Count, musicScheduleIdsToReset.Count);
    }

    private static async Task<List<ScheduleRef>> ReadRefsAsync(string scheduleDbPath, string query)
    {
        var refs = new List<ScheduleRef>();
        using var connection = new SqliteConnection($"Data Source={scheduleDbPath};Mode=ReadOnly");
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = query;
        cmd.Parameters.AddWithValue("@defaultLang", AppConstants.Media.DefaultLanguageCode);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            refs.Add(new ScheduleRef(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2)));
        }

        return refs;
    }

    private static async Task<bool> PublicationExistsAsync(
        SqliteConnection connection,
        string pubCode,
        string langCode)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(1) FROM BiblePublications bp
            JOIN Languages l ON bp.LanguageId = l.Id
            WHERE bp.PublicationCode = @pubCode AND l.LanguageCode = @langCode
            """;
        cmd.Parameters.AddWithValue("@pubCode", pubCode);
        cmd.Parameters.AddWithValue("@langCode", langCode);

        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return count > 0;
    }

    private async Task DeleteAlarmScheduleAsync(SqliteConnection connection, int scheduleId)
    {
        try
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                // Delete in dependency order (child tables first)
                await ExecuteNonQueryAsync(connection, transaction,
                    "DELETE FROM AlarmNotifications WHERE AlarmScheduleId = @id", scheduleId);
                await ExecuteNonQueryAsync(connection, transaction,
                    "DELETE FROM AlarmMusic WHERE AlarmScheduleId = @id", scheduleId);
                await ExecuteNonQueryAsync(connection, transaction,
                    "DELETE FROM BiblePublicationSchedules WHERE AlarmScheduleId = @id", scheduleId);
                await ExecuteNonQueryAsync(connection, transaction,
                    "DELETE FROM AlarmSchedules WHERE Id = @id", scheduleId);

                transaction.Commit();
                logger.Information("Deleted orphaned alarm schedule {ScheduleId}", scheduleId);
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to delete orphaned alarm schedule {ScheduleId}", scheduleId);
        }
    }

    private async Task ResetAlarmMusicAsync(SqliteConnection connection, int scheduleId)
    {
        try
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                await ExecuteNonQueryAsync(connection, transaction,
                    "DELETE FROM AlarmMusic WHERE AlarmScheduleId = @id", scheduleId);
                await ExecuteNonQueryAsync(connection, transaction,
                    "UPDATE AlarmSchedules SET MusicEnabled = 0 WHERE Id = @id", scheduleId);

                transaction.Commit();
                logger.Information("Reset music for alarm schedule {ScheduleId}", scheduleId);
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to reset music for alarm schedule {ScheduleId}", scheduleId);
        }
    }

    private static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        int scheduleId)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@id", scheduleId);
        await cmd.ExecuteNonQueryAsync();
    }
}
