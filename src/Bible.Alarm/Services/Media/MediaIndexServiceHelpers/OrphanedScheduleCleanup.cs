#nullable enable

using Bible.Alarm.Shared.Constants;
using Microsoft.Data.Sqlite;
using Serilog;

namespace Bible.Alarm.Services.Media.MediaIndexServiceHelpers;

/// <summary>
/// Runs after ad-hoc fetch (version change). Existence lookup: by lang+pub+section+track for language-based
/// pubs, or by pub+section+track for non-langed pubs; category is not used for lookup. For schedules with
/// null CategoryCode, assigns first matching category from media index using the Bible pub code.
/// </summary>
internal sealed class OrphanedScheduleCleanup(ILogger logger)
{
    private const string SqlParamPubCode = "@pubCode";
    private const string SqlParamLangCode = "@langCode";

    private record ScheduleRef(
        int AlarmScheduleId,
        string PublicationCode,
        string LanguageCode,
        string? SectionCode,
        string TrackCode);

    public async Task CleanupAsync(string mediaIndexDbPath, string scheduleDbPath)
    {
        if (!File.Exists(scheduleDbPath) || !File.Exists(mediaIndexDbPath))
        {
            return;
        }

        var bibleRefs = await ReadRefsAsync(scheduleDbPath,
            """
            SELECT AlarmScheduleId, PublicationCode, LanguageCode, SectionCode, TrackCode
            FROM BiblePublicationSchedules
            WHERE PublicationCode IS NOT NULL AND PublicationCode != '' AND LanguageCode IS NOT NULL AND TrackCode IS NOT NULL AND TrackCode != ''
            """);

        var musicRefs = await ReadRefsAsync(scheduleDbPath,
            """
            SELECT AlarmScheduleId, PublicationCode, LanguageCode, SectionCode, TrackCode
            FROM AlarmMusic
            WHERE PublicationCode IS NOT NULL AND PublicationCode != '' AND LanguageCode IS NOT NULL AND TrackCode IS NOT NULL AND TrackCode != ''
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
                if (!await TrackExistsInCatalogedAsync(mediaConn, r.PublicationCode, r.LanguageCode, r.SectionCode, r.TrackCode))
                {
                    scheduleIdsToDelete.Add(r.AlarmScheduleId);
                    logger.Warning(
                        AppConstants.Logging.OrphanedScheduleCleanupDiagnosticsLog.BibleScheduleTrackNotInFetchedTablesDeletingSchedule,
                        r.AlarmScheduleId, r.PublicationCode, r.SectionCode ?? "(none)", r.TrackCode);
                }
            }

            foreach (var r in musicRefs)
            {
                if (scheduleIdsToDelete.Contains(r.AlarmScheduleId))
                {
                    continue;
                }

                if (!await TrackExistsInCatalogedAsync(mediaConn, r.PublicationCode, r.LanguageCode, r.SectionCode, r.TrackCode))
                {
                    musicScheduleIdsToReset.Add(r.AlarmScheduleId);
                    logger.Warning(
                        AppConstants.Logging.OrphanedScheduleCleanupDiagnosticsLog.AlarmMusicScheduleTrackNotInFetchedTablesResettingMusic,
                        r.AlarmScheduleId, r.PublicationCode, r.SectionCode ?? "(none)", r.TrackCode);
                }
            }
        }

        using var scheduleConn = new SqliteConnection($"Data Source={scheduleDbPath}");
        await scheduleConn.OpenAsync();

        if (scheduleIdsToDelete.Count > 0 || musicScheduleIdsToReset.Count > 0)
        {
            foreach (var scheduleId in scheduleIdsToDelete)
            {
                await DeleteAlarmScheduleAsync(scheduleConn, scheduleId);
            }

            foreach (var scheduleId in musicScheduleIdsToReset)
            {
                await ResetAlarmMusicAsync(scheduleConn, scheduleId);
            }

            logger.Information(
                AppConstants.Logging.OrphanedScheduleCleanupDiagnosticsLog.CleanedUpOrphanedSchedulesAndResetMusicCounts,
                scheduleIdsToDelete.Count, musicScheduleIdsToReset.Count);
        }
        else
        {
            logger.Information(AppConstants.Logging.OrphanedScheduleCleanupDiagnosticsLog.AllScheduleReferencesVerifiedInFetchedTables);
        }

        await AssignCategoryCodeForNullSchedulesAsync(mediaIndexDbPath, scheduleConn);
    }

    /// <summary>
    /// For schedules with null CategoryCode, assigns first matching category from media index using the schedule's Bible pub code.
    /// </summary>
    private async Task AssignCategoryCodeForNullSchedulesAsync(
        string mediaIndexDbPath,
        SqliteConnection scheduleConn)
    {
        var nullCategoryRefs = await ReadSchedulesWithNullCategoryAndBiblePubAsync(scheduleConn);
        if (nullCategoryRefs.Count == 0)
        {
            return;
        }

        var updated = 0;
        using (var mediaConn = new SqliteConnection($"Data Source={mediaIndexDbPath};Mode=ReadOnly"))
        {
            await mediaConn.OpenAsync();

            foreach (var (scheduleId, publicationCode, languageCode) in nullCategoryRefs)
            {
                var categoryCode = await GetFirstCategoryCodeForPubAsync(mediaConn, publicationCode, languageCode);
                if (string.IsNullOrEmpty(categoryCode))
                {
                    continue;
                }

                try
                {
                    using var cmd = scheduleConn.CreateCommand();
                    cmd.CommandText = "UPDATE AlarmSchedules SET CategoryCode = @code WHERE Id = @id";
                    cmd.Parameters.AddWithValue("@code", categoryCode);
                    cmd.Parameters.AddWithValue("@id", scheduleId);
                    var n = await cmd.ExecuteNonQueryAsync();
                    if (n > 0)
                    {
                        updated++;
                        logger.Debug(AppConstants.Logging.OrphanedScheduleCleanupDiagnosticsLog.AssignedCategoryCodeFromPublication,
                            categoryCode, scheduleId, publicationCode, languageCode);
                    }
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, AppConstants.Logging.OrphanedScheduleCleanupDiagnosticsLog.FailedToAssignCategoryCodeForSchedule, scheduleId);
                }
            }
        }

        if (updated > 0)
        {
            logger.Information(AppConstants.Logging.OrphanedScheduleCleanupDiagnosticsLog.AssignedCategoryCodeFirstMatchForNullCategoryCount, updated);
        }
    }

    private static async Task<List<(int AlarmScheduleId, string PublicationCode, string LanguageCode)>> ReadSchedulesWithNullCategoryAndBiblePubAsync(
        SqliteConnection scheduleConn)
    {
        var list = new List<(int, string, string)>();
        using var cmd = scheduleConn.CreateCommand();
        cmd.CommandText = """
            SELECT bps.AlarmScheduleId, bps.PublicationCode, bps.LanguageCode
            FROM BiblePublicationSchedules bps
            JOIN AlarmSchedules a ON a.Id = bps.AlarmScheduleId
            WHERE a.CategoryCode IS NULL
              AND bps.PublicationCode IS NOT NULL AND bps.PublicationCode != ''
              AND bps.LanguageCode IS NOT NULL AND bps.LanguageCode != ''
            """;

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
        }

        return list;
    }

    /// <summary>
    /// Returns first category code for pub (by lang for language-based pub, or by pub only for non-langed pub).
    /// </summary>
    private static async Task<string?> GetFirstCategoryCodeForPubAsync(
        SqliteConnection mediaConn,
        string pubCode,
        string langCode)
    {
        using var cmd = mediaConn.CreateCommand();
        cmd.CommandText = $"""
            SELECT c.CategoryCode FROM BiblePublications p
            LEFT JOIN Languages l ON p.LanguageId = l.Id
            JOIN BiblePublicationCategories bpc ON p.Id = bpc.BiblePublicationId
            JOIN Categories c ON bpc.CategoryId = c.Id
            WHERE p.PublicationCode = {SqlParamPubCode} AND (l.LanguageCode = {SqlParamLangCode} OR p.LanguageId IS NULL)
            ORDER BY c.Id
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue(SqlParamPubCode, pubCode);
        cmd.Parameters.AddWithValue(SqlParamLangCode, langCode);

        var result = await cmd.ExecuteScalarAsync();
        return result is string s ? s : null;
    }

    private static async Task<List<ScheduleRef>> ReadRefsAsync(string scheduleDbPath, string query)
    {
        var refs = new List<ScheduleRef>();
        using var connection = new SqliteConnection($"Data Source={scheduleDbPath};Mode=ReadOnly");
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = query;

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            refs.Add(new ScheduleRef(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                await reader.IsDBNullAsync(3) ? null : reader.GetString(3),
                reader.GetString(4)));
        }

        return refs;
    }

    /// <summary>
    /// Existence lookup by: lang code + pub code + section code + track code for language-based pubs;
    /// for non-langed pubs (e.g. instrumental music) by pub code + section code + track code only.
    /// Category code is not used for this lookup.
    /// </summary>
    private static async Task<bool> TrackExistsInCatalogedAsync(
        SqliteConnection connection,
        string pubCode,
        string langCode,
        string? sectionCode,
        string trackCode)
    {
        if (string.IsNullOrEmpty(sectionCode))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"""
                SELECT COUNT(1) FROM BiblePublicationTracks t
                JOIN BiblePublications p ON t.BiblePublicationId = p.Id
                LEFT JOIN Languages l ON p.LanguageId = l.Id
                WHERE p.PublicationCode = {SqlParamPubCode} AND t.BiblePublicationSectionId IS NULL AND t.TrackCode = @trackCode
                  AND (l.LanguageCode = {SqlParamLangCode} OR p.LanguageId IS NULL)
                """;
            cmd.Parameters.AddWithValue(SqlParamPubCode, pubCode);
            cmd.Parameters.AddWithValue(SqlParamLangCode, langCode);
            cmd.Parameters.AddWithValue("@trackCode", trackCode);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"""
                SELECT COUNT(1) FROM BiblePublicationTracks t
                JOIN BiblePublicationSections s ON t.BiblePublicationSectionId = s.Id
                JOIN BiblePublications p ON s.BiblePublicationId = p.Id
                LEFT JOIN Languages l ON p.LanguageId = l.Id
                WHERE p.PublicationCode = {SqlParamPubCode} AND s.SectionCode = @sectionCode AND t.TrackCode = @trackCode
                  AND (l.LanguageCode = {SqlParamLangCode} OR p.LanguageId IS NULL)
                """;
            cmd.Parameters.AddWithValue(SqlParamPubCode, pubCode);
            cmd.Parameters.AddWithValue(SqlParamLangCode, langCode);
            cmd.Parameters.AddWithValue("@sectionCode", sectionCode);
            cmd.Parameters.AddWithValue("@trackCode", trackCode);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }
    }

    private async Task DeleteAlarmScheduleAsync(SqliteConnection connection, int scheduleId)
    {
        try
        {
            using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();
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

                await transaction.CommitAsync();
                logger.Information(AppConstants.Logging.OrphanedScheduleCleanupDiagnosticsLog.DeletedOrphanedAlarmSchedule, scheduleId);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.OrphanedScheduleCleanupDiagnosticsLog.FailedToDeleteOrphanedAlarmSchedule, scheduleId);
        }
    }

    private async Task ResetAlarmMusicAsync(SqliteConnection connection, int scheduleId)
    {
        try
        {
            using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();
            try
            {
                await ExecuteNonQueryAsync(connection, transaction,
                    "DELETE FROM AlarmMusic WHERE AlarmScheduleId = @id", scheduleId);
                await ExecuteNonQueryAsync(connection, transaction,
                    "UPDATE AlarmSchedules SET MusicEnabled = 0 WHERE Id = @id", scheduleId);

                await transaction.CommitAsync();
                logger.Information(AppConstants.Logging.OrphanedScheduleCleanupDiagnosticsLog.ResetMusicForAlarmSchedule, scheduleId);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.OrphanedScheduleCleanupDiagnosticsLog.FailedToResetMusicForAlarmSchedule, scheduleId);
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
