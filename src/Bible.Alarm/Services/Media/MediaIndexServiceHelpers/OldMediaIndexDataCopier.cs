#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Microsoft.Data.Sqlite;
using Serilog;

namespace Bible.Alarm.Services.Media.MediaIndexServiceHelpers;

/// <summary>
/// Copies minimal media data from the old media index into the new one for non-English schedule
/// references. Runs before the API fetch so schedules are preserved even without internet.
/// Uses raw SQLite with ATTACH DATABASE; maps all FKs via natural keys, never via old identity IDs.
/// </summary>
internal sealed class OldMediaIndexDataCopier(ILogger logger)
{
    private const string SqlParamPubCode = "@pubCode";
    private const string SqlParamLangCode = "@langCode";
    private const string SqlParamSectionCode = "@sectionCode";
    private const string SqlParamTrackCode = "@trackCode";

    public async Task CopyMissingAsync(
        string oldMediaIndexDbPath,
        string newMediaIndexDbPath,
        string scheduleDbPath)
    {
        if (!File.Exists(scheduleDbPath))
        {
            logger.Debug("Schedule DB not found at {Path}, skipping old media index copy", scheduleDbPath);
            return;
        }

        if (!File.Exists(oldMediaIndexDbPath))
        {
            logger.Debug("Old media index not found at {Path}, skipping copy", oldMediaIndexDbPath);
            return;
        }

        var references = await ScheduleMediaBootstrapFetcher.ReadNonEnglishSpanishReferencesAsync(scheduleDbPath);
        if (references.Count == 0)
        {
            logger.Information("No non-EnglishSpanish schedule references found; skipping old media index copy");
            return;
        }

        if (File.Exists(newMediaIndexDbPath))
        {
            references = await ScheduleMediaBootstrapFetcher.FilterToValidPubAndSectionInDiscoveryAsync(
                newMediaIndexDbPath, references);
            if (references.Count == 0)
            {
                logger.Information("No non-EnglishSpanish references with valid (discovery) pub/section; skipping copy");
                return;
            }
        }

        logger.Information(
            "Copying missing non-EnglishSpanish media for {Count} reference(s) from old media index",
            references.Count);

        var publicationGroups = references
            .GroupBy(r => (r.PublicationCode, r.LanguageCode), PublicationLookupKeyComparers.PublicationLanguage.Instance)
            .ToList();

        using var connection = new SqliteConnection($"Data Source={newMediaIndexDbPath}");
        await connection.OpenAsync();

        await OldMediaIndexSqliteAttachHelper.AttachOldMediaDatabaseAsync(connection, oldMediaIndexDbPath);

        try
        {
            foreach (var group in publicationGroups)
            {
                await CopyPublicationGroupAsync(connection, group.Key.PublicationCode, group.Key.LanguageCode, group.ToList());
            }
        }
        finally
        {
            using var detachCmd = connection.CreateCommand();
            detachCmd.CommandText = $"DETACH DATABASE {OldMediaIndexSqliteAttachHelper.OldMediaAlias}";
            await detachCmd.ExecuteNonQueryAsync();
        }

        logger.Information("Old media index copy completed");
    }

    private async Task CopyPublicationGroupAsync(
        SqliteConnection connection,
        string pubCode,
        string langCode,
        List<ScheduleMediaBootstrapFetcher.ScheduleMediaReference> references)
    {
        using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();
        try
        {
            var newLanguageId = await GetLanguageIdAsync(connection, transaction, langCode);
            if (newLanguageId == null)
            {
                logger.Debug("Language {LangCode} not found in new media index; skipping copy for {PubCode}", langCode, pubCode);
                return;
            }

            var newPubId = await GetExistingPublicationIdAsync(connection, transaction, pubCode, newLanguageId.Value);

            if (newPubId == null)
            {
                newPubId = await CopyPublicationFromOldAsync(connection, transaction, pubCode, langCode, newLanguageId.Value);
                if (newPubId == null)
                {
                    logger.Debug("Publication {PubCode}/{LangCode} not found in old media index; skipping", pubCode, langCode);
                    return;
                }

                await CopyPublicationCategoriesAsync(connection, transaction, pubCode, langCode, newPubId.Value);
            }

            var trackRefs = references
                .Where(r => !string.IsNullOrEmpty(r.TrackCode))
                .DistinctBy(r => $"{r.SectionCode ?? ""}\u001f{r.TrackCode!}", StringComparer.OrdinalIgnoreCase)
                .Select(r => (r.SectionCode, r.TrackCode!))
                .ToList();

            foreach (var (sectionCode, trackCode) in trackRefs)
            {
                await CopySectionTrackIfMissingAsync(
                    connection, transaction, pubCode, langCode,
                    newPubId.Value, sectionCode, trackCode);
            }

            await transaction.CommitAsync();
            logger.Debug("Copied publication group {PubCode}/{LangCode} from old media index", pubCode, langCode);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to copy publication group {PubCode}/{LangCode} from old media index; rolling back", pubCode, langCode);

            try
            {
                await transaction.RollbackAsync();
            }
            catch (Exception rollbackEx)
            {
                logger.Warning(rollbackEx, "Rollback failed for {PubCode}/{LangCode}", pubCode, langCode);
            }
        }
    }

    private static async Task<int?> GetLanguageIdAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string langCode)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = $"SELECT Id FROM Languages WHERE LanguageCode = {SqlParamLangCode} LIMIT 1";
        cmd.Parameters.AddWithValue(SqlParamLangCode, langCode);
        var result = await cmd.ExecuteScalarAsync();
        return result is long id ? (int)id : null;
    }

    private static async Task<int?> GetExistingPublicationIdAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string pubCode,
        int languageId)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = $"""
            SELECT Id FROM BiblePublications
            WHERE PublicationCode = {SqlParamPubCode} AND LanguageId = @langId
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue(SqlParamPubCode, pubCode);
        cmd.Parameters.AddWithValue("@langId", languageId);
        var result = await cmd.ExecuteScalarAsync();
        return result is long id ? (int)id : null;
    }

    /// <summary>
    /// Copies one BiblePublications row from old DB, remapping LanguageId via natural key.
    /// Returns the new publication Id, or null if the publication was not found in the old DB.
    /// </summary>
    private static async Task<int?> CopyPublicationFromOldAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string pubCode,
        string langCode,
        int newLanguageId)
    {
        using var readCmd = connection.CreateCommand();
        readCmd.Transaction = transaction;
        readCmd.CommandText = $"""
            SELECT op.Name, op.PublicationCode, op.IsVideo, op.IsMusic, op.CatalogType
            FROM old_media.BiblePublications op
            JOIN old_media.Languages ol ON op.LanguageId = ol.Id
            WHERE op.PublicationCode = {SqlParamPubCode} AND ol.LanguageCode = {SqlParamLangCode}
            LIMIT 1
            """;
        readCmd.Parameters.AddWithValue(SqlParamPubCode, pubCode);
        readCmd.Parameters.AddWithValue(SqlParamLangCode, langCode);

        using var reader = await readCmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        var name = reader.GetString(0);
        var publicationCode = reader.GetString(1);
        var isVideo = reader.GetBoolean(2);
        var isMusic = reader.GetBoolean(3);
        var catalogType = await reader.IsDBNullAsync(4) ? (int?)null : reader.GetInt32(4);
        await reader.CloseAsync();

        using var insertCmd = connection.CreateCommand();
        insertCmd.Transaction = transaction;
        insertCmd.CommandText = $"""
            INSERT INTO BiblePublications (Name, PublicationCode, LanguageId, IsVideo, IsMusic, CatalogType)
            VALUES (@name, {SqlParamPubCode}, @langId, @isVideo, @isMusic, @catalogType);
            SELECT last_insert_rowid();
            """;
        insertCmd.Parameters.AddWithValue("@name", name);
        insertCmd.Parameters.AddWithValue(SqlParamPubCode, publicationCode);
        insertCmd.Parameters.AddWithValue("@langId", newLanguageId);
        insertCmd.Parameters.AddWithValue("@isVideo", isVideo);
        insertCmd.Parameters.AddWithValue("@isMusic", isMusic);
        insertCmd.Parameters.AddWithValue("@catalogType", catalogType.HasValue ? (object)catalogType.Value : DBNull.Value);

        var newId = await insertCmd.ExecuteScalarAsync();
        return newId is long id ? (int)id : null;
    }

    /// <summary>
    /// Copies BiblePublicationCategories from old DB, mapping CategoryId via CategoryCode.
    /// </summary>
    private static async Task CopyPublicationCategoriesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string pubCode,
        string langCode,
        int newPubId)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = $"""
            INSERT OR IGNORE INTO BiblePublicationCategories (BiblePublicationId, CategoryId)
            SELECT @newPubId, nc.Id
            FROM old_media.BiblePublicationCategories obpc
            JOIN old_media.BiblePublications op ON obpc.BiblePublicationId = op.Id
            JOIN old_media.Languages ol ON op.LanguageId = ol.Id
            JOIN old_media.Categories oc ON obpc.CategoryId = oc.Id
            JOIN Categories nc ON nc.CategoryCode = oc.CategoryCode
            WHERE op.PublicationCode = {SqlParamPubCode} AND ol.LanguageCode = {SqlParamLangCode}
            """;
        cmd.Parameters.AddWithValue("@newPubId", newPubId);
        cmd.Parameters.AddWithValue(SqlParamPubCode, pubCode);
        cmd.Parameters.AddWithValue(SqlParamLangCode, langCode);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task CopySectionTrackIfMissingAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string pubCode,
        string langCode,
        int newPubId,
        string? sectionCode,
        string trackCode)
    {
        if (await TrackExistsInNewIndexAsync(connection, transaction, newPubId, sectionCode, trackCode))
        {
            return;
        }

        int? newSectionId = null;

        if (!string.IsNullOrEmpty(sectionCode))
        {
            newSectionId = await GetOrCopySectionAsync(connection, transaction, pubCode, langCode, newPubId, sectionCode);
            if (newSectionId == null)
            {
                logger.Debug(
                    "Section {SectionCode} for {PubCode}/{LangCode} not found in old media index; skipping track {TrackCode}",
                    sectionCode, pubCode, langCode, trackCode);
                return;
            }
        }

        var newTrackId = await CopyTrackFromOldAsync(
            connection, transaction, pubCode, langCode, newPubId, newSectionId, sectionCode, trackCode);

        if (newTrackId == null)
        {
            logger.Debug(
                "Track {TrackCode} for {PubCode}/{LangCode}/{SectionCode} not found in old media index",
                trackCode, pubCode, langCode, sectionCode ?? "(flat)");
            return;
        }

        await CopyTrackUrlFromOldAsync(connection, transaction, pubCode, langCode, sectionCode, trackCode, newTrackId.Value);
    }

    private static async Task<bool> TrackExistsInNewIndexAsync(
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
            cmd.CommandText = $"""
                SELECT COUNT(1) FROM BiblePublicationTracks
                WHERE BiblePublicationId = @pubId AND BiblePublicationSectionId IS NULL AND TrackCode = {SqlParamTrackCode}
                """;
        }
        else
        {
            cmd.CommandText = $"""
                SELECT COUNT(1) FROM BiblePublicationTracks t
                JOIN BiblePublicationSections s ON t.BiblePublicationSectionId = s.Id
                WHERE t.BiblePublicationId = @pubId AND s.SectionCode = {SqlParamSectionCode} AND t.TrackCode = {SqlParamTrackCode}
                """;
            cmd.Parameters.AddWithValue(SqlParamSectionCode, sectionCode);
        }

        cmd.Parameters.AddWithValue("@pubId", newPubId);
        cmd.Parameters.AddWithValue(SqlParamTrackCode, trackCode);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    /// <summary>
    /// Returns the new section Id: finds existing in new DB or copies from old DB.
    /// </summary>
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
        findCmd.CommandText = $"""
            SELECT Id FROM BiblePublicationSections
            WHERE BiblePublicationId = @pubId AND SectionCode = {SqlParamSectionCode}
            LIMIT 1
            """;
        findCmd.Parameters.AddWithValue("@pubId", newPubId);
        findCmd.Parameters.AddWithValue(SqlParamSectionCode, sectionCode);
        var existing = await findCmd.ExecuteScalarAsync();
        if (existing is long existingId)
        {
            return (int)existingId;
        }

        using var readCmd = connection.CreateCommand();
        readCmd.Transaction = transaction;
        readCmd.CommandText = $"""
            SELECT os.Name, os.SectionCode
            FROM old_media.BiblePublicationSections os
            JOIN old_media.BiblePublications op ON os.BiblePublicationId = op.Id
            JOIN old_media.Languages ol ON op.LanguageId = ol.Id
            WHERE op.PublicationCode = {SqlParamPubCode} AND ol.LanguageCode = {SqlParamLangCode} AND os.SectionCode = {SqlParamSectionCode}
            LIMIT 1
            """;
        readCmd.Parameters.AddWithValue(SqlParamPubCode, pubCode);
        readCmd.Parameters.AddWithValue(SqlParamLangCode, langCode);
        readCmd.Parameters.AddWithValue(SqlParamSectionCode, sectionCode);

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
        insertCmd.CommandText = $"""
            INSERT INTO BiblePublicationSections (Name, SectionCode, BiblePublicationId)
            VALUES (@name, {SqlParamSectionCode}, @newPubId);
            SELECT last_insert_rowid();
            """;
        insertCmd.Parameters.AddWithValue("@name", name);
        insertCmd.Parameters.AddWithValue(SqlParamSectionCode, readSectionCode);
        insertCmd.Parameters.AddWithValue("@newPubId", newPubId);

        var result = await insertCmd.ExecuteScalarAsync();
        return result is long newId ? (int)newId : null;
    }

    /// <summary>
    /// Copies a single track from old DB, remapping publication and section FKs.
    /// Returns the new track Id, or null if not found in old DB.
    /// </summary>
    private static async Task<int?> CopyTrackFromOldAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string pubCode,
        string langCode,
        int newPubId,
        int? newSectionId,
        string? sectionCode,
        string trackCode)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;

        if (string.IsNullOrEmpty(sectionCode))
        {
            cmd.CommandText = $"""
                SELECT ot.TrackCode, ot.Title
                FROM old_media.BiblePublicationTracks ot
                JOIN old_media.BiblePublications op ON ot.BiblePublicationId = op.Id
                JOIN old_media.Languages ol ON op.LanguageId = ol.Id
                WHERE op.PublicationCode = {SqlParamPubCode} AND ol.LanguageCode = {SqlParamLangCode}
                  AND ot.BiblePublicationSectionId IS NULL AND ot.TrackCode = {SqlParamTrackCode}
                LIMIT 1
                """;
        }
        else
        {
            cmd.CommandText = $"""
                SELECT ot.TrackCode, ot.Title
                FROM old_media.BiblePublicationTracks ot
                JOIN old_media.BiblePublicationSections os ON ot.BiblePublicationSectionId = os.Id
                JOIN old_media.BiblePublications op ON os.BiblePublicationId = op.Id
                JOIN old_media.Languages ol ON op.LanguageId = ol.Id
                WHERE op.PublicationCode = {SqlParamPubCode} AND ol.LanguageCode = {SqlParamLangCode}
                  AND os.SectionCode = {SqlParamSectionCode} AND ot.TrackCode = {SqlParamTrackCode}
                LIMIT 1
                """;
            cmd.Parameters.AddWithValue(SqlParamSectionCode, sectionCode);
        }

        cmd.Parameters.AddWithValue(SqlParamPubCode, pubCode);
        cmd.Parameters.AddWithValue(SqlParamLangCode, langCode);
        cmd.Parameters.AddWithValue(SqlParamTrackCode, trackCode);

        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        var readTrackCode = reader.GetString(0);
        var title = reader.GetString(1);
        await reader.CloseAsync();

        using var insertCmd = connection.CreateCommand();
        insertCmd.Transaction = transaction;
        insertCmd.CommandText = $"""
            INSERT INTO BiblePublicationTracks (TrackCode, Title, BiblePublicationId, BiblePublicationSectionId)
            VALUES ({SqlParamTrackCode}, @title, @pubId, @sectionId);
            SELECT last_insert_rowid();
            """;
        insertCmd.Parameters.AddWithValue(SqlParamTrackCode, readTrackCode);
        insertCmd.Parameters.AddWithValue("@title", title);
        insertCmd.Parameters.AddWithValue("@pubId", newPubId);
        insertCmd.Parameters.AddWithValue("@sectionId", newSectionId.HasValue ? (object)newSectionId.Value : DBNull.Value);

        var result = await insertCmd.ExecuteScalarAsync();
        return result is long id ? (int)id : null;
    }

    /// <summary>
    /// Copies the TrackUrl row from old DB for the given track, remapping the track FK.
    /// </summary>
    private static async Task CopyTrackUrlFromOldAsync(
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
            cmd.CommandText = $"""
                INSERT OR IGNORE INTO TrackUrls (Url, BiblePublicationTrackId)
                SELECT otu.Url, @newTrackId
                FROM old_media.TrackUrls otu
                JOIN old_media.BiblePublicationTracks ot ON otu.BiblePublicationTrackId = ot.Id
                JOIN old_media.BiblePublications op ON ot.BiblePublicationId = op.Id
                JOIN old_media.Languages ol ON op.LanguageId = ol.Id
                WHERE op.PublicationCode = {SqlParamPubCode} AND ol.LanguageCode = {SqlParamLangCode}
                  AND ot.BiblePublicationSectionId IS NULL AND ot.TrackCode = {SqlParamTrackCode}
                LIMIT 1
                """;
        }
        else
        {
            cmd.CommandText = $"""
                INSERT OR IGNORE INTO TrackUrls (Url, BiblePublicationTrackId)
                SELECT otu.Url, @newTrackId
                FROM old_media.TrackUrls otu
                JOIN old_media.BiblePublicationTracks ot ON otu.BiblePublicationTrackId = ot.Id
                JOIN old_media.BiblePublicationSections os ON ot.BiblePublicationSectionId = os.Id
                JOIN old_media.BiblePublications op ON os.BiblePublicationId = op.Id
                JOIN old_media.Languages ol ON op.LanguageId = ol.Id
                WHERE op.PublicationCode = {SqlParamPubCode} AND ol.LanguageCode = {SqlParamLangCode}
                  AND os.SectionCode = {SqlParamSectionCode} AND ot.TrackCode = {SqlParamTrackCode}
                LIMIT 1
                """;
            cmd.Parameters.AddWithValue(SqlParamSectionCode, sectionCode);
        }

        cmd.Parameters.AddWithValue("@newTrackId", newTrackId);
        cmd.Parameters.AddWithValue(SqlParamPubCode, pubCode);
        cmd.Parameters.AddWithValue(SqlParamLangCode, langCode);
        cmd.Parameters.AddWithValue(SqlParamTrackCode, trackCode);
        await cmd.ExecuteNonQueryAsync();
    }
}
