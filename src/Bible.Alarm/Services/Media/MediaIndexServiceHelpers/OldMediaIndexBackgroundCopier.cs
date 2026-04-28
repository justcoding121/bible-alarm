#nullable enable

using Microsoft.Data.Sqlite;
using Serilog;

namespace Bible.Alarm.Services.Media.MediaIndexServiceHelpers;

/// <summary>
/// Background copier that transfers ALL sections and tracks for schedule-referenced publications
/// from the old media index into the new one. Runs after critical bootstrap copy/fetch completes.
/// Transaction per section (sectioned pubs) or per publication (flat pubs) for minimal lock windows.
/// Uses PRAGMA busy_timeout to coexist with concurrent EF Core writes from ad-hoc fetches.
/// </summary>
internal sealed class OldMediaIndexBackgroundCopier(ILogger logger)
{
    public async Task CopyRemainingDataAsync(
        string oldMediaIndexDbPath,
        string newMediaIndexDbPath,
        string scheduleDbPath)
    {
        if (!File.Exists(scheduleDbPath) || !File.Exists(oldMediaIndexDbPath) || !File.Exists(newMediaIndexDbPath))
        {
            logger.Debug("Required DB files missing; skipping background copy");
            return;
        }

        var references = await ScheduleMediaBootstrapFetcher.ReadNonEnglishSpanishReferencesAsync(scheduleDbPath);
        if (references.Count == 0)
        {
            return;
        }

        var pubGroups = references
            .Select(r => (r.PublicationCode, r.LanguageCode))
            .Distinct()
            .ToList();

        logger.Information("Background copy: processing {Count} publication group(s) from old media index", pubGroups.Count);

        using var connection = new SqliteConnection($"Data Source={newMediaIndexDbPath}");
        await connection.OpenAsync();

        using (var pragmaCmd = connection.CreateCommand())
        {
            pragmaCmd.CommandText = "PRAGMA busy_timeout = 5000";
            await pragmaCmd.ExecuteNonQueryAsync();
        }

        await OldMediaIndexSqliteAttachHelper.AttachOldMediaDatabaseAsync(connection, oldMediaIndexDbPath);

        try
        {
            foreach (var (pubCode, langCode) in pubGroups)
            {
                await CopyAllDataForPublicationAsync(connection, pubCode, langCode);
            }
        }
        finally
        {
            using var detachCmd = connection.CreateCommand();
            detachCmd.CommandText = $"DETACH DATABASE {OldMediaIndexSqliteAttachHelper.OldMediaAlias}";
            await detachCmd.ExecuteNonQueryAsync();
        }

        logger.Information("Background copy completed");
    }

    private async Task CopyAllDataForPublicationAsync(
        SqliteConnection connection,
        string pubCode,
        string langCode)
    {
        var newPubId = await FindPublicationInNewDbAsync(connection, pubCode, langCode);
        if (newPubId == null)
        {
            logger.Debug("Background copy: publication {PubCode}/{LangCode} not in new DB; skipping", pubCode, langCode);
            return;
        }

        var isSectioned = await IsSectionedInOldDbAsync(connection, pubCode, langCode);

        if (isSectioned)
        {
            var sectionCodes = await ListOldSectionCodesAsync(connection, pubCode, langCode);
            foreach (var sectionCode in sectionCodes)
            {
                try
                {
                    await CopySectionWithAllTracksAsync(connection, pubCode, langCode, newPubId.Value, sectionCode);
                }
                catch (Exception ex)
                {
                    logger.Warning(ex,
                        "Background copy: failed section {SectionCode} for {PubCode}/{LangCode}; skipping",
                        sectionCode, pubCode, langCode);
                }
            }
        }
        else
        {
            try
            {
                await CopyAllFlatTracksAsync(connection, pubCode, langCode, newPubId.Value);
            }
            catch (Exception ex)
            {
                logger.Warning(ex,
                    "Background copy: failed flat tracks for {PubCode}/{LangCode}; skipping",
                    pubCode, langCode);
            }
        }
    }

    private static async Task<int?> FindPublicationInNewDbAsync(
        SqliteConnection connection,
        string pubCode,
        string langCode)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT p.Id FROM BiblePublications p
            JOIN Languages l ON p.LanguageId = l.Id
            WHERE p.PublicationCode = @pubCode AND l.LanguageCode = @langCode
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("@pubCode", pubCode);
        cmd.Parameters.AddWithValue("@langCode", langCode);
        var result = await cmd.ExecuteScalarAsync();
        return result is long id ? (int)id : null;
    }

    private static async Task<bool> IsSectionedInOldDbAsync(
        SqliteConnection connection,
        string pubCode,
        string langCode)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(1) FROM old_media.BiblePublicationSections os
            JOIN old_media.BiblePublications op ON os.BiblePublicationId = op.Id
            JOIN old_media.Languages ol ON op.LanguageId = ol.Id
            WHERE op.PublicationCode = @pubCode AND ol.LanguageCode = @langCode
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("@pubCode", pubCode);
        cmd.Parameters.AddWithValue("@langCode", langCode);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    private static async Task<List<string>> ListOldSectionCodesAsync(
        SqliteConnection connection,
        string pubCode,
        string langCode)
    {
        var codes = new List<string>();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT DISTINCT os.SectionCode
            FROM old_media.BiblePublicationSections os
            JOIN old_media.BiblePublications op ON os.BiblePublicationId = op.Id
            JOIN old_media.Languages ol ON op.LanguageId = ol.Id
            WHERE op.PublicationCode = @pubCode AND ol.LanguageCode = @langCode
            """;
        cmd.Parameters.AddWithValue("@pubCode", pubCode);
        cmd.Parameters.AddWithValue("@langCode", langCode);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            codes.Add(reader.GetString(0));
        }

        return codes;
    }

    private async Task CopySectionWithAllTracksAsync(
        SqliteConnection connection,
        string pubCode,
        string langCode,
        int newPubId,
        string sectionCode)
    {
        using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();
        try
        {
            var newSectionId = await GetOrCopySectionAsync(connection, transaction, pubCode, langCode, newPubId, sectionCode);
            if (newSectionId == null)
            {
                return;
            }

            await CopyAllTracksForSectionAsync(connection, transaction, pubCode, langCode, newPubId, newSectionId.Value, sectionCode);

            await transaction.CommitAsync();
        }
        catch
        {
            try { await transaction.RollbackAsync(); } catch { /* already logged by caller */ }
            throw;
        }
    }

    private async Task CopyAllFlatTracksAsync(
        SqliteConnection connection,
        string pubCode,
        string langCode,
        int newPubId)
    {
        using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();
        try
        {
            var trackInfos = await ReadOldFlatTracksAsync(connection, transaction, pubCode, langCode);
            var copiedCount = 0;

            foreach (var (trackCode, title) in trackInfos)
            {
                if (await TrackExistsAsync(connection, transaction, newPubId, null, trackCode))
                {
                    continue;
                }

                var newTrackId = await InsertTrackAsync(connection, transaction, newPubId, null, trackCode, title);
                if (newTrackId != null)
                {
                    await CopyTrackUrlAsync(connection, transaction, pubCode, langCode, null, trackCode, newTrackId.Value);
                    copiedCount++;
                }
            }

            await transaction.CommitAsync();

            if (copiedCount > 0)
            {
                logger.Debug("Background copy: copied {Count} flat track(s) for {PubCode}/{LangCode}", copiedCount, pubCode, langCode);
            }
        }
        catch
        {
            try { await transaction.RollbackAsync(); } catch { /* already logged by caller */ }
            throw;
        }
    }

    private static async Task<int?> GetOrCopySectionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string pubCode,
        string langCode,
        int newPubId,
        string sectionCode)
    {
        using var findCmd = connection.CreateCommand();
        findCmd.Transaction = transaction;
        findCmd.CommandText = """
            SELECT Id FROM BiblePublicationSections
            WHERE BiblePublicationId = @pubId AND SectionCode = @sectionCode
            LIMIT 1
            """;
        findCmd.Parameters.AddWithValue("@pubId", newPubId);
        findCmd.Parameters.AddWithValue("@sectionCode", sectionCode);
        var existing = await findCmd.ExecuteScalarAsync();
        if (existing is long existingId)
        {
            return (int)existingId;
        }

        using var readCmd = connection.CreateCommand();
        readCmd.Transaction = transaction;
        readCmd.CommandText = """
            SELECT os.Name, os.SectionCode
            FROM old_media.BiblePublicationSections os
            JOIN old_media.BiblePublications op ON os.BiblePublicationId = op.Id
            JOIN old_media.Languages ol ON op.LanguageId = ol.Id
            WHERE op.PublicationCode = @pubCode AND ol.LanguageCode = @langCode AND os.SectionCode = @sectionCode
            LIMIT 1
            """;
        readCmd.Parameters.AddWithValue("@pubCode", pubCode);
        readCmd.Parameters.AddWithValue("@langCode", langCode);
        readCmd.Parameters.AddWithValue("@sectionCode", sectionCode);

        using var reader = await readCmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        var name = reader.GetString(0);
        var readSectionCode = reader.GetString(1);
        await reader.CloseAsync();

        using var insertCmd = connection.CreateCommand();
        insertCmd.Transaction = transaction;
        insertCmd.CommandText = """
            INSERT INTO BiblePublicationSections (Name, SectionCode, BiblePublicationId)
            VALUES (@name, @sectionCode, @newPubId);
            SELECT last_insert_rowid();
            """;
        insertCmd.Parameters.AddWithValue("@name", name);
        insertCmd.Parameters.AddWithValue("@sectionCode", readSectionCode);
        insertCmd.Parameters.AddWithValue("@newPubId", newPubId);

        var result = await insertCmd.ExecuteScalarAsync();
        return result is long newId ? (int)newId : null;
    }

    private async Task CopyAllTracksForSectionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string pubCode,
        string langCode,
        int newPubId,
        int newSectionId,
        string sectionCode)
    {
        var trackInfos = await ReadOldSectionTracksAsync(connection, transaction, pubCode, langCode, sectionCode);
        var copiedCount = 0;

        foreach (var (trackCode, title) in trackInfos)
        {
            if (await TrackExistsAsync(connection, transaction, newPubId, sectionCode, trackCode))
            {
                continue;
            }

            var newTrackId = await InsertTrackAsync(connection, transaction, newPubId, newSectionId, trackCode, title);
            if (newTrackId != null)
            {
                await CopyTrackUrlAsync(connection, transaction, pubCode, langCode, sectionCode, trackCode, newTrackId.Value);
                copiedCount++;
            }
        }

        if (copiedCount > 0)
        {
            logger.Debug("Background copy: copied {Count} track(s) for section {SectionCode} of {PubCode}/{LangCode}",
                copiedCount, sectionCode, pubCode, langCode);
        }
    }

    private static async Task<List<(string TrackCode, string Title)>> ReadOldSectionTracksAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string pubCode,
        string langCode,
        string sectionCode)
    {
        var tracks = new List<(string, string)>();
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            SELECT ot.TrackCode, ot.Title
            FROM old_media.BiblePublicationTracks ot
            JOIN old_media.BiblePublicationSections os ON ot.BiblePublicationSectionId = os.Id
            JOIN old_media.BiblePublications op ON os.BiblePublicationId = op.Id
            JOIN old_media.Languages ol ON op.LanguageId = ol.Id
            WHERE op.PublicationCode = @pubCode AND ol.LanguageCode = @langCode AND os.SectionCode = @sectionCode
            """;
        cmd.Parameters.AddWithValue("@pubCode", pubCode);
        cmd.Parameters.AddWithValue("@langCode", langCode);
        cmd.Parameters.AddWithValue("@sectionCode", sectionCode);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tracks.Add((reader.GetString(0), reader.GetString(1)));
        }

        return tracks;
    }

    private static async Task<List<(string TrackCode, string Title)>> ReadOldFlatTracksAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string pubCode,
        string langCode)
    {
        var tracks = new List<(string, string)>();
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            SELECT ot.TrackCode, ot.Title
            FROM old_media.BiblePublicationTracks ot
            JOIN old_media.BiblePublications op ON ot.BiblePublicationId = op.Id
            JOIN old_media.Languages ol ON op.LanguageId = ol.Id
            WHERE op.PublicationCode = @pubCode AND ol.LanguageCode = @langCode
              AND ot.BiblePublicationSectionId IS NULL
            """;
        cmd.Parameters.AddWithValue("@pubCode", pubCode);
        cmd.Parameters.AddWithValue("@langCode", langCode);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tracks.Add((reader.GetString(0), reader.GetString(1)));
        }

        return tracks;
    }

    private static async Task<bool> TrackExistsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int newPubId,
        string? sectionCode,
        string trackCode)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;

        if (string.IsNullOrEmpty(sectionCode))
        {
            cmd.CommandText = """
                SELECT COUNT(1) FROM BiblePublicationTracks
                WHERE BiblePublicationId = @pubId AND BiblePublicationSectionId IS NULL AND TrackCode = @trackCode
                """;
        }
        else
        {
            cmd.CommandText = """
                SELECT COUNT(1) FROM BiblePublicationTracks t
                JOIN BiblePublicationSections s ON t.BiblePublicationSectionId = s.Id
                WHERE t.BiblePublicationId = @pubId AND s.SectionCode = @sectionCode AND t.TrackCode = @trackCode
                """;
            cmd.Parameters.AddWithValue("@sectionCode", sectionCode);
        }

        cmd.Parameters.AddWithValue("@pubId", newPubId);
        cmd.Parameters.AddWithValue("@trackCode", trackCode);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    private static async Task<int?> InsertTrackAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int newPubId,
        int? newSectionId,
        string trackCode,
        string title)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO BiblePublicationTracks (TrackCode, Title, BiblePublicationId, BiblePublicationSectionId)
            VALUES (@trackCode, @title, @pubId, @sectionId);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("@trackCode", trackCode);
        cmd.Parameters.AddWithValue("@title", title);
        cmd.Parameters.AddWithValue("@pubId", newPubId);
        cmd.Parameters.AddWithValue("@sectionId", newSectionId.HasValue ? (object)newSectionId.Value : DBNull.Value);

        var result = await cmd.ExecuteScalarAsync();
        return result is long id ? (int)id : null;
    }

    private static async Task CopyTrackUrlAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string pubCode,
        string langCode,
        string? sectionCode,
        string trackCode,
        int newTrackId)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;

        if (string.IsNullOrEmpty(sectionCode))
        {
            cmd.CommandText = """
                INSERT OR IGNORE INTO TrackUrls (Url, BiblePublicationTrackId)
                SELECT otu.Url, @newTrackId
                FROM old_media.TrackUrls otu
                JOIN old_media.BiblePublicationTracks ot ON otu.BiblePublicationTrackId = ot.Id
                JOIN old_media.BiblePublications op ON ot.BiblePublicationId = op.Id
                JOIN old_media.Languages ol ON op.LanguageId = ol.Id
                WHERE op.PublicationCode = @pubCode AND ol.LanguageCode = @langCode
                  AND ot.BiblePublicationSectionId IS NULL AND ot.TrackCode = @trackCode
                LIMIT 1
                """;
        }
        else
        {
            cmd.CommandText = """
                INSERT OR IGNORE INTO TrackUrls (Url, BiblePublicationTrackId)
                SELECT otu.Url, @newTrackId
                FROM old_media.TrackUrls otu
                JOIN old_media.BiblePublicationTracks ot ON otu.BiblePublicationTrackId = ot.Id
                JOIN old_media.BiblePublicationSections os ON ot.BiblePublicationSectionId = os.Id
                JOIN old_media.BiblePublications op ON os.BiblePublicationId = op.Id
                JOIN old_media.Languages ol ON op.LanguageId = ol.Id
                WHERE op.PublicationCode = @pubCode AND ol.LanguageCode = @langCode
                  AND os.SectionCode = @sectionCode AND ot.TrackCode = @trackCode
                LIMIT 1
                """;
            cmd.Parameters.AddWithValue("@sectionCode", sectionCode);
        }

        cmd.Parameters.AddWithValue("@newTrackId", newTrackId);
        cmd.Parameters.AddWithValue("@pubCode", pubCode);
        cmd.Parameters.AddWithValue("@langCode", langCode);
        cmd.Parameters.AddWithValue("@trackCode", trackCode);
        await cmd.ExecuteNonQueryAsync();
    }
}
