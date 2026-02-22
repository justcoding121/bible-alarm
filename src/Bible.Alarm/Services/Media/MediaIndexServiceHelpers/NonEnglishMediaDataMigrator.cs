#nullable enable

using Bible.Alarm.Shared.Constants;
using Microsoft.Data.Sqlite;
using Serilog;

namespace Bible.Alarm.Services.Media.MediaIndexServiceHelpers;

/// <summary>
/// Migrates non-English publication data from old media index to new packaged media index.
/// Preserves ad-hoc harvested content for user's existing alarm schedules during app version updates.
/// </summary>
internal sealed class NonEnglishMediaDataMigrator(ILogger logger)
{
    private record ScheduleMediaReference(
        string PublicationCode,
        string LanguageCode,
        string? SectionCode);

    public async Task MigrateAsync(
        string oldMediaDbPath,
        string newMediaDbPath,
        string scheduleDbPath)
    {
        if (!File.Exists(scheduleDbPath))
        {
            logger.Debug("Schedule DB not found at {Path}, skipping non-English media migration", scheduleDbPath);
            return;
        }

        var references = await ReadNonEnglishReferencesAsync(scheduleDbPath);
        if (references.Count == 0)
        {
            logger.Information("No non-English schedule references found; skipping media data migration");
            return;
        }

        logger.Information("Migrating non-English media data for {Count} schedule reference(s)", references.Count);

        using var connection = new SqliteConnection($"Data Source={newMediaDbPath}");
        await connection.OpenAsync();

        using (var attachCmd = connection.CreateCommand())
        {
            attachCmd.CommandText = "ATTACH DATABASE @oldPath AS old_db";
            attachCmd.Parameters.AddWithValue("@oldPath", oldMediaDbPath);
            await attachCmd.ExecuteNonQueryAsync();
        }

        try
        {
            var publicationGroups = references
                .GroupBy(r => (r.PublicationCode, r.LanguageCode))
                .ToList();

            foreach (var group in publicationGroups)
            {
                var pubCode = group.Key.PublicationCode;
                var langCode = group.Key.LanguageCode;
                var sectionCodes = group
                    .Where(r => r.SectionCode != null)
                    .Select(r => r.SectionCode!)
                    .Distinct()
                    .ToList();
                var hasFlatReference = group.Any(r => r.SectionCode == null);

                try
                {
                    await MigratePublicationAsync(
                        connection, pubCode, langCode, sectionCodes, hasFlatReference);
                }
                catch (Exception ex)
                {
                    logger.Warning(ex,
                        "Failed to migrate publication {PubCode}/{LangCode}; will require re-harvest",
                        pubCode, langCode);
                }
            }
        }
        finally
        {
            using var detachCmd = connection.CreateCommand();
            detachCmd.CommandText = "DETACH DATABASE old_db";
            await detachCmd.ExecuteNonQueryAsync();
        }

        logger.Information("Non-English media data migration completed");
    }

    private static async Task<List<ScheduleMediaReference>> ReadNonEnglishReferencesAsync(
        string scheduleDbPath)
    {
        var references = new List<ScheduleMediaReference>();
        using var connection = new SqliteConnection($"Data Source={scheduleDbPath};Mode=ReadOnly");
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT DISTINCT PublicationCode, LanguageCode, SectionCode
            FROM BiblePublicationSchedules
            WHERE LanguageCode IS NOT NULL AND LanguageCode != @defaultLang
            UNION
            SELECT DISTINCT PublicationCode, LanguageCode, SectionCode
            FROM AlarmMusic
            WHERE LanguageCode IS NOT NULL AND LanguageCode != @defaultLang
            """;
        cmd.Parameters.AddWithValue("@defaultLang", AppConstants.Media.DefaultLanguageCode);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            references.Add(new ScheduleMediaReference(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)));
        }

        return references;
    }

    private async Task MigratePublicationAsync(
        SqliteConnection connection,
        string pubCode,
        string langCode,
        List<string> sectionCodes,
        bool hasFlatReference)
    {
        if (await PublicationExistsInNewDbAsync(connection, pubCode, langCode))
        {
            logger.Debug("Publication {PubCode}/{LangCode} already exists in new media index", pubCode, langCode);
            return;
        }

        var oldPub = await ReadOldPublicationAsync(connection, pubCode, langCode);
        if (oldPub == null)
        {
            logger.Warning("Publication {PubCode}/{LangCode} not found in old media index", pubCode, langCode);
            return;
        }

        var newCategoryId = await GetIdAsync(
            connection, "SELECT Id FROM Categories WHERE CategoryCode = @val", oldPub.Value.CategoryName);
        var newLanguageId = await GetIdAsync(
            connection, "SELECT Id FROM Languages WHERE LanguageCode = @val", langCode);

        if (newCategoryId == null || newLanguageId == null)
        {
            logger.Warning(
                "Cannot migrate {PubCode}/{LangCode}: category '{Category}' or language not found in new DB",
                pubCode, langCode, oldPub.Value.CategoryName);
            return;
        }

        using var transaction = connection.BeginTransaction();
        try
        {
            var newPubId = await InsertAndGetIdAsync(connection, transaction,
                """
                INSERT INTO BiblePublications (LanguageId, PublicationCode, Name, IsVideo)
                VALUES (@p0, @p1, @p2, @p3)
                """,
                ("@p0", (object)newLanguageId.Value),
                ("@p1", pubCode),
                ("@p2", oldPub.Value.Name),
                ("@p3", oldPub.Value.IsVideo));
            using (var junctionCmd = connection.CreateCommand())
            {
                junctionCmd.Transaction = transaction;
                junctionCmd.CommandText = "INSERT INTO BiblePublicationCategories (BiblePublicationId, CategoryId) VALUES (@p0, @p1)";
                junctionCmd.Parameters.AddWithValue("@p0", newPubId);
                junctionCmd.Parameters.AddWithValue("@p1", newCategoryId.Value);
                await junctionCmd.ExecuteNonQueryAsync();
            }

            if (sectionCodes.Count > 0)
            {
                foreach (var sectionCode in sectionCodes)
                {
                    await MigrateSectionWithTracksAsync(
                        connection, transaction, oldPub.Value.Id, newPubId, sectionCode);
                }
            }

            if (hasFlatReference || sectionCodes.Count == 0)
            {
                await MigrateFlatTracksAsync(connection, transaction, oldPub.Value.Id, newPubId);
            }

            transaction.Commit();
            logger.Information("Migrated publication {PubCode}/{LangCode} to new media index", pubCode, langCode);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private async Task MigrateSectionWithTracksAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int oldPubId,
        int newPubId,
        string sectionCode)
    {
        var oldSection = await ReadOldSectionAsync(connection, transaction, oldPubId, sectionCode);
        if (oldSection == null)
        {
            logger.Warning("Section {SectionCode} not found for old pub {OldPubId}", sectionCode, oldPubId);
            return;
        }

        var newSectionId = await InsertAndGetIdAsync(connection, transaction,
            """
            INSERT INTO BiblePublicationSections (BiblePublicationId, Name, SectionCode)
            VALUES (@p0, @p1, @p2)
            """,
            ("@p0", (object)newPubId),
            ("@p1", oldSection.Value.Name),
            ("@p2", sectionCode));

        await MigrateTracksAsync(
            connection, transaction, oldPubId, newPubId, oldSection.Value.Id, newSectionId);
    }

    private async Task MigrateFlatTracksAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int oldPubId,
        int newPubId)
    {
        await MigrateTracksAsync(connection, transaction, oldPubId, newPubId, null, null);
    }

    private async Task MigrateTracksAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int oldPubId,
        int newPubId,
        int? oldSectionId,
        int? newSectionId)
    {
        var oldTracks = new List<(int Id, string TrackCode, string Title)>();

        using (var readCmd = connection.CreateCommand())
        {
            readCmd.Transaction = transaction;

            if (oldSectionId != null)
            {
                readCmd.CommandText = """
                    SELECT Id, TrackCode, Title
                    FROM old_db.BiblePublicationTracks
                    WHERE BiblePublicationId = @pubId AND BiblePublicationSectionId = @secId
                    """;
                readCmd.Parameters.AddWithValue("@pubId", oldPubId);
                readCmd.Parameters.AddWithValue("@secId", oldSectionId.Value);
            }
            else
            {
                readCmd.CommandText = """
                    SELECT Id, TrackCode, Title
                    FROM old_db.BiblePublicationTracks
                    WHERE BiblePublicationId = @pubId AND BiblePublicationSectionId IS NULL
                    """;
                readCmd.Parameters.AddWithValue("@pubId", oldPubId);
            }

            using var reader = await readCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                oldTracks.Add((
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? string.Empty : reader.GetString(2)));
            }
        }

        if (oldTracks.Count == 0)
        {
            return;
        }

        await EnsureBaseUrlsExistAsync(connection, transaction, oldTracks.Select(t => t.Id).ToList());

        foreach (var (oldTrackId, trackCode, title) in oldTracks)
        {
            var newTrackId = await InsertAndGetIdAsync(connection, transaction,
                """
                INSERT INTO BiblePublicationTracks (BiblePublicationId, BiblePublicationSectionId, TrackCode, Title)
                VALUES (@p0, @p1, @p2, @p3)
                """,
                ("@p0", (object)newPubId),
                ("@p1", (object?)newSectionId ?? DBNull.Value),
                ("@p2", trackCode),
                ("@p3", title));

            await CopyUrlParamsForTrackAsync(connection, transaction, oldTrackId, newTrackId);
        }
    }

    private static async Task EnsureBaseUrlsExistAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        List<int> oldTrackIds)
    {
        if (oldTrackIds.Count == 0)
        {
            return;
        }

        var idList = string.Join(",", oldTrackIds);

        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = $"""
            INSERT OR IGNORE INTO ApiUrls (Url, PathPrefix)
            SELECT DISTINCT old_bu.Url, old_bu.PathPrefix
            FROM old_db.UrlParams old_up
            JOIN old_db.ApiUrls old_bu ON old_up.BaseUrlId = old_bu.Id
            WHERE old_up.BiblePublicationTrackId IN ({idList})
            AND old_up.BaseUrlId IS NOT NULL
            """;

        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task CopyUrlParamsForTrackAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int oldTrackId,
        int newTrackId)
    {
        var urlParams = new List<(string? ApiUrl, string Key, string Value, bool IsQueryParam)>();

        using (var readCmd = connection.CreateCommand())
        {
            readCmd.Transaction = transaction;
            readCmd.CommandText = """
                SELECT old_bu.Url, old_up.Key, old_up.Value, old_up.IsQueryParam
                FROM old_db.UrlParams old_up
                LEFT JOIN old_db.ApiUrls old_bu ON old_up.BaseUrlId = old_bu.Id
                WHERE old_up.BiblePublicationTrackId = @oldTrackId
                """;
            readCmd.Parameters.AddWithValue("@oldTrackId", oldTrackId);

            using var reader = await readCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                urlParams.Add((
                    reader.IsDBNull(0) ? null : reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetBoolean(3)));
            }
        }

        foreach (var (apiUrl, key, value, isQueryParam) in urlParams)
        {
            int? newApiUrlId = null;
            if (apiUrl != null)
            {
                newApiUrlId = await GetIdAsync(
                    connection, "SELECT Id FROM ApiUrls WHERE Url = @val", apiUrl, transaction);
            }

            using var insertCmd = connection.CreateCommand();
            insertCmd.Transaction = transaction;
            insertCmd.CommandText = """
                INSERT INTO UrlParams (BiblePublicationTrackId, BaseUrlId, Key, Value, IsQueryParam)
                VALUES (@p0, @p1, @p2, @p3, @p4)
                """;
            insertCmd.Parameters.AddWithValue("@p0", newTrackId);
            insertCmd.Parameters.AddWithValue("@p1", (object?)newApiUrlId ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@p2", key);
            insertCmd.Parameters.AddWithValue("@p3", value);
            insertCmd.Parameters.AddWithValue("@p4", isQueryParam);

            await insertCmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task<bool> PublicationExistsInNewDbAsync(
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

    private static async Task<(int Id, string Name, bool IsVideo, string CategoryName)?> ReadOldPublicationAsync(
        SqliteConnection connection,
        string pubCode,
        string langCode)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT bp.Id, bp.Name, bp.IsVideo, c.CategoryName
            FROM old_db.BiblePublications bp
            JOIN old_db.Categories c ON bp.CategoryId = c.Id
            JOIN old_db.Languages l ON bp.LanguageId = l.Id
            WHERE bp.PublicationCode = @pubCode AND l.LanguageCode = @langCode
            """;
        cmd.Parameters.AddWithValue("@pubCode", pubCode);
        cmd.Parameters.AddWithValue("@langCode", langCode);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return (
                reader.GetInt32(0),
                reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                reader.GetBoolean(2),
                reader.GetString(3));
        }

        return null;
    }

    private static async Task<(int Id, string Name)?> ReadOldSectionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int oldPubId,
        string sectionCode)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            SELECT Id, Name FROM old_db.BiblePublicationSections
            WHERE BiblePublicationId = @pubId AND SectionCode = @code
            """;
        cmd.Parameters.AddWithValue("@pubId", oldPubId);
        cmd.Parameters.AddWithValue("@code", sectionCode);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return (reader.GetInt32(0), reader.IsDBNull(1) ? string.Empty : reader.GetString(1));
        }

        return null;
    }

    private static async Task<int?> GetIdAsync(
        SqliteConnection connection,
        string sql,
        object value,
        SqliteTransaction? transaction = null)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@val", value);

        var result = await cmd.ExecuteScalarAsync();
        return result != null && result != DBNull.Value ? Convert.ToInt32(result) : null;
    }

    private static async Task<int> InsertAndGetIdAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string insertSql,
        params (string Name, object Value)[] parameters)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = insertSql + "; SELECT last_insert_rowid();";

        foreach (var (name, value) in parameters)
        {
            cmd.Parameters.AddWithValue(name, value);
        }

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }
}
